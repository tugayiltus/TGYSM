using System.Runtime.CompilerServices;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Tgysm.Application.Abstractions;
using Tgysm.Application.Common;
using Tgysm.Domain.Common;
using Tgysm.Domain.Reports;

namespace Tgysm.Application.Reports;

/// <summary>
/// Worker-side use case behind <c>ExportRunnerJob</c> (spec §7.3, ADR-0014, ADR-0020): claims due export jobs with a row lease and
/// builds each file in its own DI scope — the same rows as the drill-down, read under the creator's scope as it is now
/// (<see cref="ReportScope.ViewerForAsync"/>), written through <see cref="IReportFileWriter"/> into <see cref="IFileStore"/>. A creator
/// who is inactive or no longer IT staff/admin fails the job without a file; so does a job that grew past
/// <see cref="ReportExportLimits.JobMaxRows"/>. A failed write leaves no file behind, and the stored error is sanitized.
/// </summary>
public sealed class RunExportJobsHandler(
    IExportJobClaimer claimer,
    IServiceScopeFactory scopes,
    TimeProvider clock,
    ILogger<RunExportJobsHandler> logger)
{
    /// <summary>Claims up to <paramref name="batch"/> jobs and runs them one by one; returns how many it finished (ready or failed).</summary>
    public async Task<int> HandleAsync(int batch, TimeSpan lease, CancellationToken cancellationToken)
    {
        var claimed = await claimer.ClaimAsync(batch, lease, cancellationToken);
        var finished = 0;
        foreach (var job in claimed)
        {
            // The batch lease covers the whole batch and can run out before a later job is reached; renew this one first, with
            // the lease the batch claim wrote as the ownership token, and skip it when another runner holds it now.
            var token = job.LockedUntil is { } batchToken
                ? await claimer.RenewAsync(job.Id, batchToken, lease, cancellationToken)
                : null;
            if (token is null)
            {
                logger.LogInformation("Export job {JobId} was taken over by another run; skipping it.", job.Id);
                continue;
            }

            await using var scope = scopes.CreateAsyncScope();
            try
            {
                if (await RunOneAsync(scope.ServiceProvider, job.Id, token.Value, cancellationToken))
                {
                    finished++;
                }
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogError(exception, "Export job {JobId} failed.", job.Id);
                if (await TryRecordFailureAsync(job.Id, token.Value, ErrorSanitizer.Sanitize(exception, ExportJob.LastErrorMaxLength), cancellationToken))
                {
                    finished++;
                }
            }
        }

        return finished;
    }

    private async Task<bool> RunOneAsync(IServiceProvider scoped, Guid jobId, DateTimeOffset token, CancellationToken cancellationToken)
    {
        var db = scoped.GetRequiredService<IAppDbContext>();
        var job = await db.ExportJobs.SingleAsync(j => j.Id == jobId, cancellationToken);
        if (!IsHeldBy(job, token))
        {
            return false;
        }

        ReportViewer viewer;
        try
        {
            viewer = await scoped.GetRequiredService<ReportScope>().ViewerForAsync(job.CreatedBy, cancellationToken);
        }
        catch (ForbiddenException)
        {
            job.Fail(ErrorCodes.Forbidden, clock.GetUtcNow());
            await db.SaveChangesAsync(cancellationToken);
            return true;
        }

        job.Start(clock.GetUtcNow());
        await db.SaveChangesAsync(cancellationToken);

        var filter = ReportFilter.FromJson(job.Filters);
        var opened = scoped.GetRequiredService<ReportRowSource>().Open(viewer, job.Report, job.Metric, job.Bucket, job.Part, filter);
        if (await ReportExportRows.CountAsync(opened, cancellationToken) > ReportExportLimits.JobMaxRows)
        {
            job.Fail(ErrorCodes.ReportsExportTooLarge, clock.GetUtcNow());
            await db.SaveChangesAsync(cancellationToken);
            return true;
        }

        var names = await ReportExportNames.ResolveAsync(db, filter, job.Bucket, cancellationToken);
        var content = ReportExportRows.ContentOf(
            opened, job.Report, job.Metric, job.Bucket, job.Part, filter, GetReportHandler.ScopeLabelOf(viewer, job.Report), names);
        var tally = new RowTally();
        content = content with { Rows = Counted(content.Rows, tally) };

        var writer = scoped.GetRequiredService<IReportFileWriter>();
        var files = scoped.GetRequiredService<IFileStore>();
        var fileKey = await files.SaveAsync((output, ct) => writer.WriteAsync(content, output, ct), cancellationToken);
        try
        {
            job.Complete(fileKey, tally.Count, clock.GetUtcNow());
            await db.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            // The job never pointed at this file (another runner took over, or the save failed): no orphan may stay behind.
            files.Delete(fileKey);
            throw;
        }

        return true;
    }

    /// <summary>
    /// Records the failure in a fresh scope that reloads the job: the failed run's context may still track unsaved state. Only the run
    /// still holding the job records it; a failure to record is logged, never rethrown, so the rest of the batch still runs.
    /// </summary>
    private async Task<bool> TryRecordFailureAsync(Guid jobId, DateTimeOffset token, string error, CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = scopes.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
            var job = await db.ExportJobs.SingleAsync(j => j.Id == jobId, cancellationToken);
            if (!IsHeldBy(job, token))
            {
                logger.LogWarning("Export job {JobId} is no longer held by this run; its failure is not recorded.", jobId);
                return false;
            }

            job.Fail(error, clock.GetUtcNow());
            await db.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "Recording the failure of export job {JobId} failed.", jobId);
            return false;
        }
    }

    private static bool IsHeldBy(ExportJob job, DateTimeOffset token) =>
        job.Status is ExportJobStatus.Pending or ExportJobStatus.Running && job.LockedUntil == token;

    private sealed class RowTally
    {
        public int Count { get; set; }
    }

    private static async IAsyncEnumerable<IReadOnlyList<object?>> Counted(
        IAsyncEnumerable<IReadOnlyList<object?>> rows, RowTally tally, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var row in rows.WithCancellation(cancellationToken))
        {
            tally.Count++;
            yield return row;
        }
    }
}
