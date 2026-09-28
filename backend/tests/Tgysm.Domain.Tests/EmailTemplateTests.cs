using Tgysm.Domain.Common;
using Tgysm.Domain.Notifications;

namespace Tgysm.Domain.Tests;

public sealed class EmailTemplateTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static EmailTemplate CreateTicketCreatedTemplate() =>
        EmailTemplate.Create(NotificationEventTypes.TicketCreated, "Subject", "Body", Guid.NewGuid(), Now);

    [Fact]
    public void Update_throws_when_the_subject_exceeds_the_maximum_length()
    {
        var template = CreateTicketCreatedTemplate();
        var tooLong = new string('a', EmailTemplate.SubjectMaxLength + 1);

        var exception = Assert.Throws<DomainException>(() => template.Update(tooLong, "Body", Guid.NewGuid(), Now));

        Assert.Equal(ErrorCodes.TooLong, exception.Code);
    }

    [Fact]
    public void Update_throws_when_the_body_exceeds_the_maximum_length()
    {
        var template = CreateTicketCreatedTemplate();
        var tooLong = new string('a', EmailTemplate.BodyMaxLength + 1);

        var exception = Assert.Throws<DomainException>(() => template.Update("Subject", tooLong, Guid.NewGuid(), Now));

        Assert.Equal(ErrorCodes.TooLong, exception.Code);
    }

    [Fact]
    public void Update_throws_when_the_text_uses_a_variable_not_allowed_for_the_event()
    {
        var template = CreateTicketCreatedTemplate();

        // actionUrl is a known variable, but only for account events, not ticket_created.
        var exception = Assert.Throws<DomainException>(() => template.Update("{{actionUrl}}", "Body", Guid.NewGuid(), Now));

        Assert.Equal(ErrorCodes.EmailTemplateVariableNotAllowed, exception.Code);
    }

    [Fact]
    public void Update_throws_when_the_text_uses_an_unknown_variable()
    {
        var template = CreateTicketCreatedTemplate();

        var exception = Assert.Throws<DomainException>(() => template.Update("{{doesNotExist}}", "Body", Guid.NewGuid(), Now));

        Assert.Equal(ErrorCodes.EmailTemplateUnknownVariable, exception.Code);
    }

    [Fact]
    public void Update_accepts_text_within_length_using_only_allowed_variables()
    {
        var template = CreateTicketCreatedTemplate();

        template.Update("[{{ticketNumber}}] {{subject}}", "Durum: {{status}}", Guid.NewGuid(), Now);

        Assert.Equal("[{{ticketNumber}}] {{subject}}", template.Subject);
        Assert.Equal("Durum: {{status}}", template.Body);
    }
}
