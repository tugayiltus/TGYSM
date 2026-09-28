using Tgysm.Domain.Notifications;

namespace Tgysm.Domain.Tests;

public sealed class TemplateRendererTests
{
    [Fact]
    public void Render_replaces_known_variables_with_given_values()
    {
        var values = new Dictionary<string, string> { ["ticketNumber"] = "INC-1", ["subject"] = "Arıza" };

        var result = TemplateRenderer.Render("[{{ticketNumber}}] {{subject}}", values);

        Assert.Equal("[INC-1] Arıza", result);
    }

    [Fact]
    public void Render_treats_spacing_variants_of_the_same_placeholder_identically()
    {
        var values = new Dictionary<string, string> { ["ticketNumber"] = "INC-1" };

        Assert.Equal("INC-1", TemplateRenderer.Render("{{ticketNumber}}", values));
        Assert.Equal("INC-1", TemplateRenderer.Render("{{ ticketNumber }}", values));
        Assert.Equal("INC-1", TemplateRenderer.Render("{{ticketNumber }}", values));
        Assert.Equal("INC-1", TemplateRenderer.Render("{{ ticketNumber}}", values));
    }

    [Fact]
    public void Render_leaves_a_missing_value_as_empty_text()
    {
        var result = TemplateRenderer.Render("Hello {{recipientName}}!", new Dictionary<string, string>());

        Assert.Equal("Hello !", result);
    }

    [Fact]
    public void Render_does_not_recursively_expand_a_replacement_value_that_itself_contains_a_placeholder()
    {
        // A naive "replace, then re-scan the result" implementation would expand {{ticketNumber}} a second time.
        var values = new Dictionary<string, string> { ["subject"] = "{{ticketNumber}}", ["ticketNumber"] = "INC-1" };

        var result = TemplateRenderer.Render("{{subject}}", values);

        Assert.Equal("{{ticketNumber}}", result);
    }

    [Fact]
    public void Validate_returns_no_bad_names_when_every_variable_is_allowed_for_the_event()
    {
        var badNames = TemplateRenderer.Validate("[{{ticketNumber}}] {{subject}} - {{status}}", NotificationEventTypes.TicketCreated);

        Assert.Empty(badNames);
    }

    [Fact]
    public void Validate_flags_a_completely_unknown_variable_name()
    {
        var badNames = TemplateRenderer.Validate("{{doesNotExist}}", NotificationEventTypes.TicketCreated);

        Assert.Equal(["doesNotExist"], badNames);
    }

    [Fact]
    public void Validate_flags_a_known_variable_not_allowed_for_this_event()
    {
        var badNames = TemplateRenderer.Validate("{{actionUrl}}", NotificationEventTypes.TicketCreated);

        Assert.Equal(["actionUrl"], badNames);
    }

    [Fact]
    public void Validate_treats_spacing_variants_the_same_as_the_plain_form()
    {
        Assert.Equal(["actionUrl"], TemplateRenderer.Validate("{{ actionUrl }}", NotificationEventTypes.TicketCreated));
    }

    [Fact]
    public void Validate_reports_each_bad_name_once_even_if_it_appears_twice()
    {
        var badNames = TemplateRenderer.Validate("{{doesNotExist}} and {{doesNotExist}} again", NotificationEventTypes.TicketCreated);

        Assert.Equal(["doesNotExist"], badNames);
    }
}
