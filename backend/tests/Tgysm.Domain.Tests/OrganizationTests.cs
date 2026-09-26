using Tgysm.Domain.Common;
using Tgysm.Domain.Organization;

namespace Tgysm.Domain.Tests;

public sealed class OrganizationTests
{
    [Fact]
    public void Department_normalizes_name_with_turkish_rules()
    {
        var department = Department.Create("  İnsan Kaynakları ", null);

        Assert.Equal("İnsan Kaynakları", department.Name);
        Assert.Equal("insan kaynaklari", department.NormalizedName);
        Assert.True(department.IsActive);
    }

    [Fact]
    public void Location_cannot_be_its_own_parent()
    {
        var location = Location.Create("Merkez Kampüs", null);

        var error = Assert.Throws<DomainException>(() => location.Update("Merkez Kampüs", location.Id));

        Assert.Equal(ErrorCodes.LocationCycle, error.Code);
    }

    [Fact]
    public void Team_deactivate_and_reactivate_toggle_state()
    {
        var team = Team.Create("Saha Destek");

        team.Deactivate();
        Assert.False(team.IsActive);

        team.Reactivate();
        Assert.True(team.IsActive);
    }
}
