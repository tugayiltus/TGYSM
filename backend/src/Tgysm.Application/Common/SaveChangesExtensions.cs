using Tgysm.Application.Abstractions;
using Tgysm.Domain.Common;

namespace Tgysm.Application.Common;

internal static class SaveChangesExtensions
{
    /// <summary>Saves and turns a generic unique violation (a lost race with a parallel request) into a specific code.</summary>
    public static async Task SaveTranslatingDuplicateAsync(this IAppDbContext db, string code, string message, CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DomainException exception) when (exception.Code == ErrorCodes.Duplicate)
        {
            throw new DomainException(code, message, exception);
        }
    }
}
