namespace Tgysm.Application.Common;

/// <summary>The resource existed but is no longer available (an expired export file); the API answers 410 with <see cref="Code"/>.</summary>
public sealed class GoneException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}
