using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Tgysm.Application.Abstractions;
using Tgysm.Application.Common;
using Tgysm.Domain.Audit;
using Tgysm.Domain.Common;
using Tgysm.Domain.Sla;

namespace Tgysm.Application.Features.Sla.Admin;

public sealed record AddHolidayCommand(DateOnly Date, string Name);

internal sealed class AddHolidayValidator : AbstractValidator<AddHolidayCommand>
{
    public AddHolidayValidator() => RuleFor(c => c.Name).NotEmpty().MaximumLength(Holiday.NameMaxLength);
}

/// <summary>Marks a date as closed for future accumulation (ADR-0018); one holiday per date.</summary>
public sealed class AddHolidayHandler(IAppDbContext db, IValidator<AddHolidayCommand> validator, IAuditLog audit)
{
    private const string DuplicateMessage = "A holiday is already defined for this date.";

    public async Task<HolidayItem> HandleAsync(AddHolidayCommand command, CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(command, cancellationToken);
        if (await db.Holidays.AnyAsync(h => h.Date == command.Date, cancellationToken))
        {
            throw new DomainException(ErrorCodes.SlaHolidayExists, DuplicateMessage);
        }

        var holiday = Holiday.Create(command.Date, command.Name);
        db.Holidays.Add(holiday);
        audit.Record(AuditActions.SlaHolidayAdded, AuditEntityTypes.Holiday, holiday.Id, newValues: new { holiday.Date, holiday.Name });
        await db.SaveTranslatingDuplicateAsync(ErrorCodes.SlaHolidayExists, DuplicateMessage, cancellationToken);
        return HolidayItem.From(holiday);
    }
}
