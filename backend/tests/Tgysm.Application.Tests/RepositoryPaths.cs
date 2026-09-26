namespace Tgysm.Application.Tests;

internal static class RepositoryPaths
{
    private const string MarkerFile = "tgysm.md";

    public static string Root { get; } = FindRoot();

    public static string Locale(string fileName) =>
        Path.Combine(Root, "frontend", "src", "shared", "i18n", "locales", "tr", fileName);

    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, MarkerFile)))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException($"Repository root ({MarkerFile}) not found.");
    }
}
