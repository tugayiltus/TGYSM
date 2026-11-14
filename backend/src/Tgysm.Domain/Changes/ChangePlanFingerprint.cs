using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Tgysm.Domain.Changes;

/// <summary>
/// Hash of what an approver signed off: window, affected assets, risk, impact, the three plans and the Uygulama
/// tasks (ADR-0032). A changed hash means the approval no longer applies.
/// </summary>
public static class ChangePlanFingerprint
{
    private const char LengthSeparator = ':';

    /// <summary>Length written for a null value, so null and empty text give different fingerprints.</summary>
    private const int NullLength = -1;

    public static string Compute(ChangeRequest change, IEnumerable<Guid> assetIds, IEnumerable<Guid> implementationTaskIds)
    {
        var builder = new StringBuilder();
        Append(builder, Instant(change.WindowStart));
        Append(builder, Instant(change.WindowEnd));
        AppendIds(builder, assetIds);
        Append(builder, change.Risk.ToString());
        Append(builder, change.Impact.ToString());
        Append(builder, change.ImplementationPlan);
        Append(builder, change.VerificationPlan);
        Append(builder, change.BackoutPlan);
        AppendIds(builder, implementationTaskIds);

        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString())));
    }

    private static string? Instant(DateTimeOffset? value) =>
        value?.UtcDateTime.ToString("O", CultureInfo.InvariantCulture);

    private static void AppendIds(StringBuilder builder, IEnumerable<Guid> ids)
    {
        var sorted = ids.Distinct().Order().ToList();
        Append(builder, sorted.Count.ToString(CultureInfo.InvariantCulture));
        foreach (var id in sorted)
        {
            Append(builder, id.ToString("D", CultureInfo.InvariantCulture));
        }
    }

    // Length prefix keeps field boundaries unambiguous; null and empty text are told apart by NullLength.
    private static void Append(StringBuilder builder, string? value)
    {
        builder.Append(value is null ? NullLength : value.Length).Append(LengthSeparator).Append(value);
    }
}
