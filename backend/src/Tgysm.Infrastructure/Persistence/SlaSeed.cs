using System.Globalization;
using System.Text;
using Tgysm.Domain.Sla;
using Tgysm.Domain.Tickets;
using Tgysm.Infrastructure.Persistence.Converters;

namespace Tgysm.Infrastructure.Persistence;

/// <summary>
/// The sample SLA setup every installation starts with (spec §1: "samples, not commitments"): Istanbul calendar,
/// Monday–Friday 09:00–18:00, policy version 1. The <c>SlaSetup</c> migration runs <see cref="Sql"/>; integration tests
/// run it again after wiping the tables so they share one definition of the seed.
/// </summary>
public static class SlaSeed
{
    public const string TimeZoneId = "Europe/Istanbul";
    public const int WarningPercent = 75;

    private const string SystemActor = "00000000-0000-0000-0000-000000000000";
    private const string PolicyVersionId = "6a1d2f4e-3b7c-4e58-9a0d-5c2b8e1f7a43";
    private const int WorkingDayMinutes = 9 * 60;
    private const string OpensAt = "09:00";
    private const string ClosesAt = "18:00";

    private static readonly DayOfWeek[] WorkingDays =
        [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday];

    // first response minutes, resolution minutes (business time)
    private static readonly Dictionary<Priority, (int FirstResponse, int Resolution)> Targets = new()
    {
        [Priority.P1] = (30, 4 * 60),
        [Priority.P2] = (60, 8 * 60),
        [Priority.P3] = (4 * 60, 3 * WorkingDayMinutes),
        [Priority.P4] = (8 * 60, 5 * WorkingDayMinutes),
    };

    /// <summary>Hold reasons that pause the resolution timer only.</summary>
    private static readonly HoldReason[] PausingReasons = [HoldReason.User, HoldReason.ManagerApproval];

    public static string Sql { get; } = Build();

    private static string Build()
    {
        var sql = new StringBuilder();
        sql.AppendLine(CultureInfo.InvariantCulture,
            $"INSERT INTO business_calendar (id, time_zone, updated_by, updated_at) VALUES (gen_random_uuid(), '{TimeZoneId}', '{SystemActor}', now());");

        foreach (var day in WorkingDays)
        {
            sql.AppendLine(CultureInfo.InvariantCulture,
                $"INSERT INTO business_hours (id, weekday, starts_at, ends_at) VALUES (gen_random_uuid(), {(int)day}, '{OpensAt}', '{ClosesAt}');");
        }

        sql.AppendLine(CultureInfo.InvariantCulture,
            $"INSERT INTO sla_policy_versions (id, number, created_by, created_at, is_current) VALUES ('{PolicyVersionId}', 1, '{SystemActor}', now(), true);");

        foreach (var kind in Enum.GetValues<TicketKind>())
        {
            foreach (var (priority, minutes) in Targets)
            {
                AppendTarget(sql, kind, priority, SlaMetric.FirstResponse, minutes.FirstResponse);
                AppendTarget(sql, kind, priority, SlaMetric.Resolution, minutes.Resolution);
            }
        }

        foreach (var reason in PausingReasons)
        {
            sql.AppendLine(CultureInfo.InvariantCulture,
                $"INSERT INTO sla_pause_rules (id, policy_version_id, hold_reason, pauses_first_response, pauses_resolution) VALUES (gen_random_uuid(), '{PolicyVersionId}', '{Name(reason)}', false, true);");
        }

        return sql.ToString();
    }

    private static void AppendTarget(StringBuilder sql, TicketKind kind, Priority priority, SlaMetric metric, int minutes) =>
        sql.AppendLine(CultureInfo.InvariantCulture,
            $"INSERT INTO sla_targets (id, policy_version_id, ticket_kind, priority, request_type_id, metric, target_minutes, warning_percent) VALUES (gen_random_uuid(), '{PolicyVersionId}', '{Name(kind)}', '{Name(priority)}', NULL, '{Name(metric)}', {minutes}, {WarningPercent});");

    private static string Name<TEnum>(TEnum value)
        where TEnum : struct, Enum =>
        SnakeCaseEnumConversion<TEnum>.Converter.ConvertToProvider(value)!.ToString()!;
}
