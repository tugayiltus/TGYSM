using FluentValidation;
using Tgysm.Application.Features.Setup;
using Tgysm.Domain.Common;

namespace Tgysm.Api.Cli;

/// <summary><c>Tgysm.Api setup-admin --email … --first-name … --last-name …</c> (ADR-0007).</summary>
internal static class SetupAdminCommand
{
    public const string Name = "setup-admin";

    private const string EmailOption = "--email";
    private const string FirstNameOption = "--first-name";
    private const string LastNameOption = "--last-name";
    private const int Success = 0;
    private const int Failure = 1;
    private const int OptionPairSize = 2;

    public static async Task<int> RunAsync(IServiceProvider services, IReadOnlyList<string> args, TextWriter output, TextWriter error)
    {
        var options = ParseOptions(args);
        if (!options.TryGetValue(EmailOption, out var email)
            || !options.TryGetValue(FirstNameOption, out var firstName)
            || !options.TryGetValue(LastNameOption, out var lastName))
        {
            await error.WriteLineAsync($"Usage: {Name} {EmailOption} <email> {FirstNameOption} <name> {LastNameOption} <name>");
            return Failure;
        }

        await using var scope = services.CreateAsyncScope();
        var handler = scope.ServiceProvider.GetRequiredService<SetupFirstAdminHandler>();
        try
        {
            var result = await handler.HandleAsync(new SetupFirstAdminCommand(email, firstName, lastName), CancellationToken.None);
            await output.WriteLineAsync($"First admin created: {email}");
            await output.WriteLineAsync($"Temporary password (shown once): {result.TemporaryPassword}");
            return Success;
        }
        catch (DomainException exception) when (exception.Code == ErrorCodes.SetupAlreadyCompleted)
        {
            await error.WriteLineAsync("Setup refused: an account already exists.");
            return Failure;
        }
        catch (ValidationException exception)
        {
            var details = string.Join("; ", exception.Errors.Select(e => $"{e.PropertyName}: {e.ErrorMessage}"));
            await error.WriteLineAsync($"Invalid input: {details}");
            return Failure;
        }
    }

    private static Dictionary<string, string> ParseOptions(IReadOnlyList<string> args)
    {
        var options = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var index = 0; index + 1 < args.Count; index += OptionPairSize)
        {
            options[args[index]] = args[index + 1];
        }

        return options;
    }
}
