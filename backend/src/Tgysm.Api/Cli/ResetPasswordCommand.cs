using Microsoft.EntityFrameworkCore;
using Tgysm.Application.Abstractions;
using Tgysm.Application.Features.Users;
using Tgysm.Domain.Users;

namespace Tgysm.Api.Cli;

/// <summary>
/// <c>Tgysm.Api reset-password --email …</c>: issues a temporary password from the server console, for an operator
/// who cannot sign in (for example the only admin forgot the password). The user must change it at next sign-in.
/// </summary>
internal static class ResetPasswordCommand
{
    public const string Name = "reset-password";

    private const string EmailOption = "--email";
    private const int Success = 0;
    private const int Failure = 1;
    private const int OptionPairSize = 2;

    public static async Task<int> RunAsync(IServiceProvider services, IReadOnlyList<string> args, TextWriter output, TextWriter error)
    {
        if (args.Count != OptionPairSize || args[0] != EmailOption)
        {
            await error.WriteLineAsync($"Usage: {Name} {EmailOption} <email>");
            return Failure;
        }

        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
        var normalized = User.NormalizeEmail(args[1]);
        var userId = await db.Users.Where(u => u.NormalizedEmail == normalized).Select(u => (Guid?)u.Id).SingleOrDefaultAsync();
        if (userId is not { } id)
        {
            await error.WriteLineAsync($"No account with email {args[1]}.");
            return Failure;
        }

        var result = await scope.ServiceProvider.GetRequiredService<ResetUserPasswordHandler>().HandleAsync(new ResetUserPasswordCommand(id), CancellationToken.None);
        await output.WriteLineAsync($"Password reset: {args[1]}");
        await output.WriteLineAsync($"Temporary password (shown once): {result.TemporaryPassword}");
        return Success;
    }
}
