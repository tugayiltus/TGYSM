using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Tgysm.Application;
using Tgysm.Application.Abstractions;
using Tgysm.Application.Common;
using Tgysm.Infrastructure;
using Tgysm.Infrastructure.Email;
using Tgysm.Worker.Jobs;

namespace Tgysm.Worker;

internal static class Program
{
    private static async Task Main(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);
        builder.Services.AddApplication();
        builder.Services.AddInfrastructure();
        // AddApplication() registers audit/handler types that need ICurrentActor; Tgysm.Api supplies HttpCurrentActor
        // for a request, the Worker has no signed-in user so it supplies its own system actor instead.
        builder.Services.AddScoped<ICurrentActor, WorkerCurrentActor>();
        builder.Services.AddOptions<WorkerOptions>().BindConfiguration(WorkerOptions.SectionName);
        builder.Services.AddOptions<AppOptions>()
            .BindConfiguration(AppOptions.SectionName)
            .Validate(o => AppOptionsValidation.IsValidPublicBaseUrl(o.PublicBaseUrl), AppOptionsValidation.PublicBaseUrlErrorMessage)
            .ValidateOnStart();
        // AddInfrastructure() already binds EmailOptions (so IEmailSender can resolve); this adds the Worker's own
        // fail-fast requirement (spec §3.5: "Email:Mode missing → worker refuses to start") on top of it.
        builder.Services.AddOptions<EmailOptions>()
            .Validate(o => o.Mode is EmailModes.Smtp or EmailModes.Pickup, $"Email:Mode must be '{EmailModes.Smtp}' or '{EmailModes.Pickup}'.")
            .ValidateOnStart();
        builder.Services.AddScoped<HeartbeatRecorder>();

        builder.Services.AddHostedService<OutboxDispatcherJob>();
        builder.Services.AddHostedService<EmailSenderJob>();
        builder.Services.AddHostedService<SlaMonitorJob>();
        builder.Services.AddHostedService<CleanupJob>();
        // Remaining jobs (export runner) register themselves here in later tasks.

        var app = builder.Build();
        await app.RunAsync();
    }
}
