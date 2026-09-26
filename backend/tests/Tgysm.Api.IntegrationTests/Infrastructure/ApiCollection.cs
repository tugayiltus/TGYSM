namespace Tgysm.Api.IntegrationTests.Infrastructure;

/// <summary>All API tests share one host and database and run one after another.</summary>
[CollectionDefinition(Name)]
public sealed class ApiCollection : ICollectionFixture<ApiFactory>
{
    public const string Name = "api";
}
