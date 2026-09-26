using Tgysm.Api;
using Tgysm.Api.Cli;
using Tgysm.Application;
using Tgysm.Infrastructure;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddApplication();
builder.Services.AddInfrastructure();
builder.Services.AddApiServices(builder.Configuration);

var app = builder.Build();

if (args is [SetupAdminCommand.Name, .. var commandArgs])
{
    return await SetupAdminCommand.RunAsync(app.Services, commandArgs, Console.Out, Console.Error);
}

app.UseApiPipeline();
app.MapApi();
await app.RunAsync();
return 0;

public partial class Program;
