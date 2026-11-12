namespace Tgysm.Application.Common;

/// <summary>The company directory (Active Directory or Microsoft Entra) did not answer; the API returns 503 (ADR-0030).</summary>
public sealed class DirectoryUnavailableException() : Exception("The directory is unavailable.");
