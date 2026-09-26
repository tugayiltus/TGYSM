using Microsoft.EntityFrameworkCore;
using Tgysm.Application.Abstractions;
using Tgysm.Application.Common;
using Tgysm.Domain.Audit;

namespace Tgysm.Application.Features.Users;

public sealed record GetUserQuery(Guid Id);

public sealed class GetUserHandler(IAppDbContext db)
{
    public async Task<UserDetail> HandleAsync(GetUserQuery query, CancellationToken cancellationToken)
    {
        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Id == query.Id, cancellationToken)
            ?? throw new NotFoundException(AuditEntityTypes.User);
        return UserMapping.ToDetail(user);
    }
}
