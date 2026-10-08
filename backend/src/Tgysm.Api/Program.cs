using Tgysm.Api;
using Tgysm.Api.Cli;
using Tgysm.Application;
using Tgysm.Infrastructure;
using Tgysm.Infrastructure.Files;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddApplication();
builder.Services.AddInfrastructure();
// Logs the file-store root the Worker must share; fails startup when it is missing outside Development.
builder.Services.AddHostedService<FileStoreRootLog>();
builder.Services.AddApiServices(builder.Configuration);

var app = builder.Build();

if (args is [SetupAdminCommand.Name, .. var commandArgs])
{
    return await SetupAdminCommand.RunAsync(app.Services, commandArgs, Console.Out, Console.Error);
}

if (args is [ResetPasswordCommand.Name, .. var resetArgs])
{
    return await ResetPasswordCommand.RunAsync(app.Services, resetArgs, Console.Out, Console.Error);
}

app.UseApiPipeline();
app.MapApi();
await app.RunAsync();
return 0;

public partial class Program;
