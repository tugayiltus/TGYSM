using System.Reflection;
using NetArchTest.Rules;

namespace Tgysm.ArchitectureTests;

public sealed class LayerDependencyTests
{
    private const string ApplicationNamespace = "Tgysm.Application";
    private const string InfrastructureNamespace = "Tgysm.Infrastructure";
    private const string ApiNamespace = "Tgysm.Api";
    private const string EntityFrameworkNamespace = "Microsoft.EntityFrameworkCore";
    private const string AspNetCoreNamespace = "Microsoft.AspNetCore";
    private const string NpgsqlNamespace = "Npgsql";
    private const string HttpNamespace = "System.Net.Http";

    private static readonly Assembly Domain = typeof(Tgysm.Domain.AssemblyMarker).Assembly;
    private static readonly Assembly Application = typeof(Tgysm.Application.AssemblyMarker).Assembly;
    private static readonly Assembly Infrastructure = typeof(Tgysm.Infrastructure.AssemblyMarker).Assembly;
    private static readonly Assembly Worker = typeof(Tgysm.Worker.AssemblyMarker).Assembly;

    [Fact]
    public void Domain_depends_on_no_other_layer_or_framework()
    {
        var result = Types.InAssembly(Domain).ShouldNot().HaveDependencyOnAny(
            ApplicationNamespace, InfrastructureNamespace, ApiNamespace,
            EntityFrameworkNamespace, AspNetCoreNamespace, NpgsqlNamespace, HttpNamespace).GetResult();

        Assert.True(result.IsSuccessful, Describe(result));
    }

    [Fact]
    public void Application_does_not_depend_on_infrastructure_api_npgsql_or_aspnetcore()
    {
        var result = Types.InAssembly(Application).ShouldNot().HaveDependencyOnAny(
            InfrastructureNamespace, ApiNamespace, NpgsqlNamespace, AspNetCoreNamespace, HttpNamespace).GetResult();

        Assert.True(result.IsSuccessful, Describe(result));
    }

    [Fact]
    public void Infrastructure_does_not_depend_on_api()
    {
        var result = Types.InAssembly(Infrastructure).ShouldNot().HaveDependencyOn(ApiNamespace).GetResult();

        Assert.True(result.IsSuccessful, Describe(result));
    }

    [Fact]
    public void Worker_does_not_depend_on_api()
    {
        var result = Types.InAssembly(Worker).ShouldNot().HaveDependencyOn(ApiNamespace).GetResult();

        Assert.True(result.IsSuccessful, Describe(result));
    }

    private static string Describe(NetArchTest.Rules.TestResult result) =>
        "Violating types: " + string.Join(", ", result.FailingTypeNames ?? []);
}
