using Tgysm.Domain.Assets;
using Tgysm.Domain.Common;

namespace Tgysm.Domain.Tests;

public sealed class AssetImportJobTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 26, 10, 0, 0, TimeSpan.Zero);
    private static readonly Guid Actor = Guid.CreateVersion7();

    private static AssetImportJob Job(int errorRows) =>
        AssetImportJob.Create("  cihazlar.csv ", totalRows: 3, validRows: 3 - errorRows, errorRows, "[]", "[]", Actor, Now);

    [Fact]
    public void New_job_is_previewed()
    {
        var job = Job(errorRows: 0);

        Assert.Equal(AssetImportStatus.Previewed, job.Status);
        Assert.Equal("cihazlar.csv", job.FileName);
        Assert.Null(job.CommittedAt);
    }

    [Fact]
    public void Commit_marks_the_job_once()
    {
        var job = Job(errorRows: 0);
        var later = Now.AddMinutes(1);

        Assert.True(job.Commit(later));
        Assert.False(job.Commit(later.AddMinutes(1)));
        Assert.Equal(AssetImportStatus.Committed, job.Status);
        Assert.Equal(later, job.CommittedAt);
    }

    [Fact]
    public void Job_with_error_rows_cannot_be_committed()
    {
        var job = Job(errorRows: 1);

        var error = Assert.Throws<DomainException>(() => job.Commit(Now));

        Assert.Equal(ErrorCodes.ImportHasErrors, error.Code);
        Assert.Equal(AssetImportStatus.Previewed, job.Status);
    }
}
