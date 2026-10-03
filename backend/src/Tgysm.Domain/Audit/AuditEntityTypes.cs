namespace Tgysm.Domain.Audit;

/// <summary>Entity type names used in audit rows and NotFound errors. Turkish labels live under "entityTypes" in admin.json.</summary>
public static class AuditEntityTypes
{
    public const string User = "user";
    public const string Department = "department";
    public const string Location = "location";
    public const string Team = "team";
    public const string Asset = "asset";
    public const string AssetImport = "asset_import";
    public const string Category = "category";
    public const string RequestType = "request_type";
    public const string Ticket = "ticket";
    public const string Approval = "approval";
    public const string Attachment = "attachment";
    public const string EmailTemplate = "email_template";
    public const string EmailDelivery = "email_delivery";
    public const string SlaCalendar = "sla_calendar";
    public const string Holiday = "holiday";
    public const string SlaPolicy = "sla_policy";
    public const string KnowledgeArticle = "knowledge_article";
    public const string Report = "report";
    public const string ExportJob = "export_job";
}
