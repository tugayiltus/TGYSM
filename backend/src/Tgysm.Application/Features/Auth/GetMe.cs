using Microsoft.EntityFrameworkCore;
using Tgysm.Application.Abstractions;
using Tgysm.Application.Common;
using Tgysm.Domain.Audit;
using Tgysm.Domain.Users;

namespace Tgysm.Application.Features.Auth;

public sealed record MeDto(
    Guid Id,
    string FirstName,
    string LastName,
    string Email,
    UserRole Role,
    bool MustChangePassword,
    string? DepartmentName,
    string? LocationName);

public sealed class GetMeHandler(IAppDbContext db, ICurrentActor actor)
{
    public async Task<MeDto> HandleAsync(CancellationToken cancellationToken)
    {
        var userId = actor.UserId ?? throw new AuthenticationFailedException();

        var me = await (
            from user in db.Users.AsNoTracking()
            where user.Id == userId
            join department in db.Departments on user.DepartmentId equals (Guid?)department.Id into departments
            from department in departments.DefaultIfEmpty()
            join location in db.Locations on user.LocationId equals (Guid?)location.Id into locations
            from location in locations.DefaultIfEmpty()
            select new MeDto(
                user.Id,
                user.FirstName,
                user.LastName,
                user.Email,
                user.Role,
                user.MustChangePassword,
                department == null ? null : department.Name,
                location == null ? null : location.Name))
            .SingleOrDefaultAsync(cancellationToken);

        return me ?? throw new NotFoundException(AuditEntityTypes.User);
    }
}
