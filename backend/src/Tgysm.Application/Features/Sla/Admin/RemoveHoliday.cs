using Microsoft.EntityFrameworkCore;
using Tgysm.Application.Abstractions;
using Tgysm.Application.Common;
using Tgysm.Domain.Audit;

namespace Tgysm.Application.Features.Sla.Admin;

public sealed record RemoveHolidayCommand(Guid Id);

/// <summary>Reopens a holiday date for future accumulation; time already accumulated is not recomputed (ADR-0018).</summary>
public sealed class RemoveHolidayHandler(IAppDbContext db, IAuditLog audit)
{
    public async Task HandleAsync(RemoveHolidayCommand command, CancellationToken cancellationToken)
    {
        var holiday = await db.Holidays.SingleOrDefaultAsync(h => h.Id == command.Id, cancellationToken)
            ?? throw new NotFoundException(AuditEntityTypes.Holiday);
        db.Holidays.Remove(holiday);
        audit.Record(AuditActions.SlaHolidayRemoved, AuditEntityTypes.Holiday, holiday.Id, oldValues: new { holiday.Date, holiday.Name });
        await db.SaveChangesAsync(cancellationToken);
    }
}
