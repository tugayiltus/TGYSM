using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Tgysm.Application.Abstractions;
using Tgysm.Application.Common;
using Tgysm.Domain.Audit;
using Tgysm.Domain.Common;
using Tgysm.Domain.Organization;

namespace Tgysm.Application.Features.Organization.Teams;

internal static class TeamQueries
{
    public const string NameTakenMessage = "Another team uses this name.";

    public static IRuleBuilderOptions<T, string> ValidTeamName<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty().MaximumLength(Team.NameMaxLength);

    public static async Task<TeamDetail> LoadDetailAsync(IAppDbContext db, Guid teamId, CancellationToken cancellationToken)
    {
        var team = await db.Teams.AsNoTracking().SingleOrDefaultAsync(t => t.Id == teamId, cancellationToken)
            ?? throw new NotFoundException(AuditEntityTypes.Team);

        var members = await (
            from membership in db.TeamMemberships.AsNoTracking()
            where membership.TeamId == teamId
            join user in db.Users on membership.UserId equals user.Id
            orderby user.LastName, user.FirstName, user.Id
            select new TeamMemberItem(user.Id, user.FirstName, user.LastName, user.Email, user.Role, user.IsActive))
            .ToListAsync(cancellationToken);

        return new TeamDetail(team.Id, team.Name, team.IsActive, team.Version, members);
    }

    public static async Task EnsureNameFreeAsync(IAppDbContext db, string name, Guid? exceptId, CancellationToken cancellationToken)
    {
        var key = TurkishText.ToSearchKey(name);
        if (await db.Teams.AnyAsync(t => t.NormalizedName == key && t.Id != exceptId, cancellationToken))
        {
            throw new DomainException(ErrorCodes.TeamNameTaken, NameTakenMessage);
        }
    }
}
