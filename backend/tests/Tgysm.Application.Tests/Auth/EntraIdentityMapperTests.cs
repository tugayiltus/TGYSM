using System.Security.Claims;
using Tgysm.Application.Features.Auth.Directory;
using Tgysm.Domain.Auth;

namespace Tgysm.Application.Tests.Auth;

public sealed class EntraIdentityMapperTests
{
    private const string Tenant = "6f1c2b5e-1a2b-4c3d-8e9f-0a1b2c3d4e5f";
    private static readonly string[] Allowed = ["sirket.com"];

    private static ClaimsPrincipal Principal(params (string Type, string Value)[] claims) =>
        new(new ClaimsIdentity(claims.Select(c => new Claim(c.Type, c.Value)), "test"));

    private static (string, string)[] Base(string tenant = Tenant) =>
        [("tid", tenant), ("oid", "oid-1"), ("given_name", "Ahmet"), ("family_name", "Yılmaz")];

    [Fact]
    public void A_company_identity_maps_to_a_directory_identity()
    {
        var identity = EntraIdentityMapper.Map(Principal([.. Base(), ("email", "Ahmet@Sirket.com")]), Tenant, Allowed);

        Assert.Equal(new DirectoryIdentity(LoginMethod.Entra, "oid-1", "Ahmet@Sirket.com", "Ahmet", "Yılmaz"), identity);
    }

    [Fact]
    public void Another_tenant_is_never_mapped_even_with_an_allowed_email() =>
        Assert.Null(EntraIdentityMapper.Map(Principal([.. Base("00000000-0000-0000-0000-000000000001"), ("email", "ahmet@sirket.com")]), Tenant, Allowed));

    [Fact]
    public void The_email_falls_back_to_preferred_username_then_upn()
    {
        Assert.Equal("ahmet@sirket.com", EntraIdentityMapper.Map(Principal([.. Base(), ("preferred_username", "ahmet@sirket.com")]), Tenant, Allowed)?.Email);
        Assert.Equal("ahmet@sirket.com", EntraIdentityMapper.Map(Principal([.. Base(), ("upn", "ahmet@sirket.com")]), Tenant, Allowed)?.Email);
    }

    [Fact]
    public void A_domain_outside_the_allowed_list_is_refused() =>
        Assert.Null(EntraIdentityMapper.Map(Principal([.. Base(), ("email", "ahmet@baska.com")]), Tenant, Allowed));

    [Fact]
    public void An_empty_allowed_list_accepts_any_domain_of_the_tenant() =>
        Assert.NotNull(EntraIdentityMapper.Map(Principal([.. Base(), ("email", "ahmet@baska.com")]), Tenant, []));

    [Fact]
    public void Names_fall_back_to_splitting_the_display_name()
    {
        var identity = EntraIdentityMapper.Map(Principal(("tid", Tenant), ("oid", "oid-1"), ("name", "Ahmet Can Yılmaz"), ("email", "ahmet@sirket.com")), Tenant, Allowed);

        Assert.Equal(("Ahmet Can", "Yılmaz"), (identity?.FirstName, identity?.LastName));
    }

    [Fact]
    public void Missing_object_id_is_refused() =>
        Assert.Null(EntraIdentityMapper.Map(Principal(("tid", Tenant), ("email", "ahmet@sirket.com")), Tenant, Allowed));
}
