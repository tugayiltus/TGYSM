using Tgysm.Domain.Catalog;
using Tgysm.Domain.Common;
using Tgysm.Domain.Organization;

namespace Tgysm.Domain.Tests;

public sealed class CatalogTests
{
    [Fact]
    public void Category_names_are_normalized_turkish_insensitively()
    {
        var category = Category.Create(" Donanım ", null, CategoryScope.Both);

        Assert.Equal(("Donanım", "donanim"), (category.Name, category.NormalizedName));
        Assert.True(category.IsActive);
        Assert.True(category.AppliesTo(Tickets.TicketKind.Incident) && category.AppliesTo(Tickets.TicketKind.Request));
        Assert.False(Category.Create("Kurulum", null, CategoryScope.Request).AppliesTo(Tickets.TicketKind.Incident));
    }

    [Fact]
    public void Category_cannot_be_its_own_parent()
    {
        var category = Category.Create("Donanım", null, CategoryScope.Both);

        Assert.Equal(ErrorCodes.CategoryTooDeep, Assert.Throws<DomainException>(() => category.Update("Donanım", category.Id, CategoryScope.Both)).Code);
    }

    [Fact]
    public void Request_type_keeps_its_approval_flag()
    {
        var type = RequestType.Create(" Yeni cihaz ", requiresApproval: true);
        type.Update("Yeni cihaz talebi", requiresApproval: false);
        type.Deactivate();

        Assert.Equal(("Yeni cihaz talebi", "yeni cihaz talebi", false, false), (type.Name, type.NormalizedName, type.RequiresApproval, type.IsActive));
    }

    [Fact]
    public void Team_intake_flag_can_be_set_and_cleared()
    {
        var team = Team.Create("Servis Masası");

        team.MarkDefaultIntake();
        Assert.True(team.IsDefaultIntake);
        team.ClearDefaultIntake();
        Assert.False(team.IsDefaultIntake);
    }
}
