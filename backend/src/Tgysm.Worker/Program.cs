using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Tgysm.Application;
using Tgysm.Application.Abstractions;
using Tgysm.Infrastructure;

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
        builder.Services.AddScoped<HeartbeatRecorder>();

        // Concrete jobs (outbox dispatcher, SLA evaluator, export runner, cleanup) register themselves here in later tasks.

        var app = builder.Build();
        await app.RunAsync();
    }
}
