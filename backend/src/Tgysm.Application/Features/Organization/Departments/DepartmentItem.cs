namespace Tgysm.Application.Features.Organization.Departments;

public sealed record DepartmentItem(Guid Id, string Name, bool IsActive, Guid? ApproverUserId, string? ApproverName, uint Version);
