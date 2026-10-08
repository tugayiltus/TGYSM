using Tgysm.Domain.Reports;

namespace Tgysm.Domain.Tests.Reports;

/// <summary><see cref="ExportJob"/>'s life cycle (spec §7.3): pending → running → ready → expired, or failed.</summary>
public sealed class ExportJobTests
{
    private static readonly DateTimeOffset Now = new(2026, 3, 10, 9, 0, 0, TimeSpan.Zero);
    private static readonly Guid Creator = Guid.NewGuid();
    private const string Filters = "{\"from\":\"2026-03-01\"}";
    private const string FileKey = "0123456789abcdef0123456789abcdef";

    private static ExportJob NewJob() => ExportJob.Create(ReportName.Incidents, "volume", "8+", ReportDrillPart.Numerator, Filters, Creator, Now);

    private static ExportJob Running()
    {
        var job = NewJob();
        job.Start(Now.AddMinutes(1));
        return job;
    }

    [Fact]
    public void Create_keeps_what_was_asked_and_starts_pending()
    {
        var job = NewJob();

        Assert.Equal(ExportJobStatus.Pending, job.Status);
        Assert.Equal(ReportName.Incidents, job.Report);
        Assert.Equal("volume", job.Metric);
        Assert.Equal("8+", job.Bucket);
        Assert.Equal(ReportDrillPart.Numerator, job.Part);
        Assert.Equal(Filters, job.Filters);
        Assert.Equal(Creator, job.CreatedBy);
        Assert.Equal(Now, job.CreatedAt);
        Assert.Null(job.FileKey);
        Assert.Null(job.RowCount);
        Assert.Null(job.ExpiresAt);
    }

    [Fact]
    public void Complete_makes_the_file_ready_for_the_file_lifetime()
    {
        var job = Running();
        var done = Now.AddMinutes(2);

        job.Complete(FileKey, 42, done);

        Assert.Equal(ExportJobStatus.Ready, job.Status);
        Assert.Equal(FileKey, job.FileKey);
        Assert.Equal(42, job.RowCount);
        Assert.Equal(done + ReportExportLimits.FileLifetime, job.ExpiresAt);
        Assert.Null(job.LockedUntil);
        Assert.True(job.IsDownloadable(done));
        Assert.False(job.IsDownloadable(done + ReportExportLimits.FileLifetime));
    }

    [Fact]
    public void A_crashed_run_can_be_started_again()
    {
        var job = Running();

        job.Start(Now.AddMinutes(30));

        Assert.Equal(ExportJobStatus.Running, job.Status);
        Assert.Equal(Now.AddMinutes(30), job.StartedAt);
    }

    [Fact]
    public void Each_start_counts_an_attempt_until_the_attempts_are_used_up()
    {
        var job = NewJob();
        Assert.Equal(0, job.Attempts);
        Assert.False(job.AttemptsExhausted);

        for (var attempt = 1; attempt <= ReportExportLimits.MaxAttempts; attempt++)
        {
            Assert.False(job.AttemptsExhausted);
            job.Start(Now.AddMinutes(attempt));
            Assert.Equal(attempt, job.Attempts);
        }

        Assert.True(job.AttemptsExhausted);
    }

    [Fact]
    public void Fail_keeps_no_file_and_cuts_the_error_to_its_column()
    {
        var job = Running();

        job.Fail(new string('x', ExportJob.LastErrorMaxLength + 10), Now.AddMinutes(2));

        Assert.Equal(ExportJobStatus.Failed, job.Status);
        Assert.Null(job.FileKey);
        Assert.Equal(ExportJob.LastErrorMaxLength, job.LastError!.Length);
        Assert.Null(job.LockedUntil);
    }

    [Fact]
    public void A_pending_job_can_fail_before_it_starts()
    {
        var job = NewJob();

        job.Fail("forbidden", Now);

        Assert.Equal(ExportJobStatus.Failed, job.Status);
    }

    [Fact]
    public void Expire_drops_the_file_key_once_the_lifetime_is_over()
    {
        var job = Running();
        job.Complete(FileKey, 1, Now);

        job.Expire(Now + ReportExportLimits.FileLifetime);

        Assert.Equal(ExportJobStatus.Expired, job.Status);
        Assert.Null(job.FileKey);
        Assert.False(job.IsDownloadable(Now));
    }

    [Fact]
    public void Transitions_out_of_order_are_refused()
    {
        Assert.Throws<InvalidOperationException>(() => NewJob().Complete(FileKey, 1, Now));
        Assert.Throws<InvalidOperationException>(() => NewJob().Expire(Now));

        var ready = Running();
        ready.Complete(FileKey, 1, Now);
        Assert.Throws<InvalidOperationException>(() => ready.Start(Now));
        Assert.Throws<InvalidOperationException>(() => ready.Fail("x", Now));
        Assert.Throws<InvalidOperationException>(() => ready.Expire(Now)); // still inside its lifetime
    }
}
