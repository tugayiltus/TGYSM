namespace Tgysm.Api.IntegrationTests.Infrastructure;

[CollectionDefinition(Name)]
public sealed class DatabaseCollection : ICollectionFixture<PostgresDatabase>
{
    public const string Name = "database";
}
