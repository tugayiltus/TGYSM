using System.Globalization;
using Tgysm.Application;
using Tgysm.Application.Abstractions;
using Tgysm.Application.Features.Pilot;
using Tgysm.Infrastructure;

namespace Tgysm.Api.Cli;

/// <summary>
/// <c>Tgysm.Api seed-pilot [--scale &lt;factor&gt;]</c> (spec §5.1): fills an empty development or pilot database with
/// realistic data through the application handlers. Refuses in any other environment and once a ticket exists.
/// The handlers run in a provider of their own whose actor and clock the seed moves (<see cref="PilotSeedActor"/>,
/// <see cref="PilotSeedClock"/>); the API's HTTP actor and system clock are never replaced.
/// </summary>
internal static class SeedPilotCommand
{
    public const string Name = "seed-pilot";
    public const string PilotEnvironment = "Pilot";

    private const string ScaleOption = "--scale";
    private const double DefaultScale = 1;
    private const double MaxScale = 10;
    private const int Success = 0;
    private const int Failure = 1;

    private const string RecoveryHint =
        "Local: run tools/seed-pilot.sh --fresh. Compose: restore the pre-seed backup, or recreate the database volume and migrate, then seed again.";

    private static readonly string BreachMailWarning =
        "Warning: seeded notifications were closed, but the Worker's SLA monitor will later raise breach notifications for open seeded tickets, " +
        $"addressed to @{PilotSeed.EmailDomain}. While only seed data exists, use a relay that drops that domain or EMAIL_MODE=Pickup.";

    private static readonly string[] AllowedEnvironments = [Environments.Development, PilotEnvironment];

    public static async Task<int> RunAsync(IServiceProvider services, IReadOnlyList<string> args, TextWriter output, TextWriter error)
    {
        var environment = services.GetRequiredService<IHostEnvironment>();
        if (!AllowedEnvironments.Contains(environment.EnvironmentName, StringComparer.Ordinal))
        {
            await error.WriteLineAsync(
                $"Seed refused: {Name} runs only in {string.Join(" or ", AllowedEnvironments)}, not in {environment.EnvironmentName}.");
            return Failure;
        }

        if (!TryParseScale(args, out var scale))
        {
            await error.WriteLineAsync($"Usage: {Name} [{ScaleOption} <factor greater than 0, at most {MaxScale.ToString(CultureInfo.InvariantCulture)}>]");
            return Failure;
        }

        await using var seedServices = BuildSeedServices(services.GetRequiredService<IConfiguration>(), environment);
        var seed = seedServices.GetRequiredService<PilotSeed>();
        if (await seed.HasTicketsAsync(CancellationToken.None))
        {
            await error.WriteLineAsync("Seed refused: the database already has tickets.");
            return Failure;
        }

        PilotSeedResult result;
        try
        {
            result = await seed.RunAsync(scale, CancellationToken.None);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // What was written stays (each handler call commits); the seed is for a fresh database, so start over.
            await error.WriteLineAsync($"Seed failed; the data written so far stays. {RecoveryHint}");
            await error.WriteLineAsync(exception.ToString());
            return Failure;
        }

        await output.WriteLineAsync(result.AdminCreated
            ? $"Admin created: {result.AdminEmail}"
            : $"Existing admin used: {result.AdminEmail} (password unchanged)");
        await output.WriteLineAsync($"Pilot password for every seeded account (shown once): {result.Password}");
        foreach (var (role, count) in result.UsersByRole.OrderBy(r => r.Key))
        {
            await output.WriteLineAsync($"Users ({role}): {count}");
        }

        await output.WriteLineAsync($"Assets: {result.Assets}");
        foreach (var (status, count) in result.TicketsByStatus.OrderBy(s => s.Key))
        {
            await output.WriteLineAsync($"Tickets ({status}): {count}");
        }

        await output.WriteLineAsync($"Comments: {result.Comments}");
        await output.WriteLineAsync($"Articles: {result.Articles}");
        await output.WriteLineAsync($"Audit events: {result.AuditEvents}");
        await output.WriteLineAsync(BreachMailWarning);
        return Success;
    }

    private static bool TryParseScale(IReadOnlyList<string> args, out double scale)
    {
        scale = DefaultScale;
        return args switch
        {
            [] => true,
            [ScaleOption, var value] => double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out scale) && scale > 0 && scale <= MaxScale,
            _ => false,
        };
    }

    /// <summary>The same Application + Infrastructure composition as the Worker, with the seed's actor and clock.</summary>
    private static ServiceProvider BuildSeedServices(IConfiguration configuration, IHostEnvironment environment)
    {
        var seedServices = new ServiceCollection();
        seedServices.AddSingleton(configuration);
        seedServices.AddSingleton(environment);
        seedServices.AddLogging();
        seedServices.AddSingleton<PilotSeedClock>();
        seedServices.AddSingleton<TimeProvider>(provider => provider.GetRequiredService<PilotSeedClock>());
        seedServices.AddSingleton<PilotSeedActor>();
        seedServices.AddSingleton<ICurrentActor>(provider => provider.GetRequiredService<PilotSeedActor>());
        seedServices.AddApplication();
        seedServices.AddInfrastructure();
        seedServices.AddSingleton<PilotSeed>();
        return seedServices.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = false });
    }
}
