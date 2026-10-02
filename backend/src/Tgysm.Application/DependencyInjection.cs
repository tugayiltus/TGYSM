using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Tgysm.Application.Abstractions;
using Tgysm.Application.Audit;
using Tgysm.Application.Common;
using Tgysm.Application.Features.Organization;
using Tgysm.Application.Features.Users;
using Tgysm.Application.Notifications;
using Tgysm.Application.Security;
using Tgysm.Application.Sla;

namespace Tgysm.Application;

public static class DependencyInjection
{
    private const string HandlerSuffix = "Handler";

    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        var assembly = typeof(DependencyInjection).Assembly;

        services.AddValidatorsFromAssembly(assembly, includeInternalTypes: true);

        var handlerTypes = assembly.GetTypes()
            .Where(type => type is { IsClass: true, IsAbstract: false, IsPublic: true }
                && type.Name.EndsWith(HandlerSuffix, StringComparison.Ordinal));
        foreach (var handlerType in handlerTypes)
        {
            services.AddScoped(handlerType);
        }

        services.AddScoped<IAuditLog, AuditLog>();
        services.AddScoped<INotificationOutbox, NotificationOutbox>();
        services.AddScoped<RecipientResolver>();
        services.AddScoped<NotificationContextLoader>();
        services.AddScoped<ICompanyClock, CompanyClock>();
        services.AddSingleton<ITemporaryPasswordGenerator, TemporaryPasswordGenerator>();
        services.AddScoped<PasswordTokenIssuer>();
        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<OrganizationReferences>();
        services.AddScoped<LastAdminGuard>();
        services.AddScoped<CalendarProvider>();
        services.AddScoped<PolicyProvider>();
        services.AddScoped<SlaTracker>();
        services.AddScoped<SlaCalendarReplanner>();
        services.AddScoped<TicketSlaReader>();
        services.AddScoped<Features.Tickets.TicketScope>();
        services.AddScoped<Reports.ReportScope>();
        services.AddScoped<Reports.TicketReportSource>();
        services.AddScoped<Features.Tickets.TicketFactory>();
        services.AddScoped<Features.Tickets.TicketWork>();
        return services;
    }
}
