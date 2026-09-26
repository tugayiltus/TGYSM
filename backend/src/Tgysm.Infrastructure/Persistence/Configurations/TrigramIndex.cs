namespace Tgysm.Infrastructure.Persistence.Configurations;

/// <summary>pg_trgm GIN index settings for substring search.</summary>
internal static class TrigramIndex
{
    public const string Extension = "pg_trgm";
    public const string Method = "gin";
    public const string Operators = "gin_trgm_ops";
}
