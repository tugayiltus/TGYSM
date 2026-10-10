using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Tgysm.Application.Abstractions;
using Tgysm.Application.Features.Approvals;
using Tgysm.Application.Features.Assets;
using Tgysm.Application.Features.Assets.Catalog;
using Tgysm.Application.Features.Assets.Lifecycle;
using Tgysm.Application.Features.Auth;
using Tgysm.Application.Features.Catalog;
using Tgysm.Application.Features.Knowledge;
using Tgysm.Application.Features.Organization.Departments;
using Tgysm.Application.Features.Organization.Locations;
using Tgysm.Application.Features.Organization.Teams;
using Tgysm.Application.Features.Setup;
using Tgysm.Application.Features.Tickets;
using Tgysm.Application.Features.Users;
using Tgysm.Domain.Assets;
using Tgysm.Domain.Catalog;
using Tgysm.Domain.Tickets;
using Tgysm.Domain.Users;

namespace Tgysm.Application.Features.Pilot;

/// <summary>Where a seeded ticket ends up; the plan cycles through all of them so every status exists at any scale.</summary>
internal enum PilotTicketPlan
{
    Open,
    OpenAssigned,
    InProgress,
    OnHold,
    Completed,
    Cancelled,
}

/// <summary>One run of the pilot seed: the random draws, the created ids and the moving "now".</summary>
internal sealed class PilotSeedSession(
    IServiceScopeFactory scopes, PilotSeedActor actor, PilotSeedClock clock, double scale, string password, DateTimeOffset realNow, CancellationToken cancellationToken)
{
    // Organisation, catalog and assets are set up this long before the first ticket, one step per handler call.
    private const int SetupLeadDays = 30;
    private static readonly TimeSpan SetupStep = TimeSpan.FromMinutes(1);

    // Nothing is written closer to the real "now" than this, so no record lies in the future.
    private static readonly TimeSpan CreationMargin = TimeSpan.FromHours(2);
    private static readonly TimeSpan ActionMargin = TimeSpan.FromMinutes(1);

    // Tickets are opened during office hours (UTC; 08:00-18:00 in Istanbul).
    private const int OfficeOpensHourUtc = 5;
    private const int OfficeMinutes = 10 * 60;
    private const int MinutesPerDay = 24 * 60;

    // Purchase dates lie up to about four years before setup; warranties run two or three years.
    private const int MinPurchaseAgeDays = 30;
    private const int MaxPurchaseAgeDays = 1_400;
    private static readonly int[] WarrantyYears = [2, 3];

    // Asset end states, in percent; whatever is left stays in stock.
    private const int AssignedPercent = 45;
    private const int MaintenancePercent = 8;
    private const int LostPercent = 4;
    private const int RetiredPercent = 8;
    private const int Hundred = 100;

    // Ticket shape, in percent.
    private const int RequestPercent = 30;
    private const int DeviceLinkPercent = 30;
    private const int PendingApprovalPercent = 50;
    private const int RequesterReplyPercent = 50;
    private const int PriorityChangePercent = 40;
    private const int TransferPercent = 15;
    private const int InternalNotePercent = 60;
    private const int HoldBeforeCompletePercent = 25;
    private const int WorkerReplyPercent = 70;
    private const int RootCausePercent = 30;

    // Minutes between the steps of one ticket (from, to).
    private static readonly (int Min, int Max) ApprovalDelay = (30, 24 * 60);
    private static readonly (int Min, int Max) ClaimDelay = (5, 180);
    private static readonly (int Min, int Max) StartDelay = (5, 240);
    private static readonly (int Min, int Max) CommentDelay = (10, 600);
    private static readonly (int Min, int Max) HoldDuration = (120, 2 * MinutesPerDay);
    private static readonly (int Min, int Max) WorkDuration = (30, 3 * MinutesPerDay);
    private static readonly (int Min, int Max) ArticlePublishDelay = (30, 240);

    // One block of the status cycle: mostly completed work, some of every other state.
    private static readonly PilotTicketPlan[] PlanBlock =
    [
        .. Enumerable.Repeat(PilotTicketPlan.Completed, 11),
        .. Enumerable.Repeat(PilotTicketPlan.InProgress, 3),
        .. Enumerable.Repeat(PilotTicketPlan.Cancelled, 2),
        .. Enumerable.Repeat(PilotTicketPlan.OnHold, 2),
        PilotTicketPlan.Open,
        PilotTicketPlan.OpenAssigned,
    ];

    private static readonly HoldReason[] HoldReasons = [HoldReason.User, HoldReason.Vendor, HoldReason.ThirdParty];
    private static readonly TransferReason[] TransferReasons = [TransferReason.ExpertiseNeeded, TransferReason.Location, TransferReason.OtherTeamResponsibility];

    // People and devices sit in locations without children.
    private static readonly PilotLocation[] LeafLocations =
        [.. PilotSeedData.Locations.Where(l => PilotSeedData.Locations.All(c => c.Parent != l.Name))];

    private readonly Random _random = new(PilotSeed.RandomSeed);
    private readonly DateTimeOffset _windowStart = realNow.AddDays(-PilotSeedVolumes.DaysBack);
    private readonly Dictionary<string, Guid> _departments = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Guid> _locations = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Guid> _categories = new(StringComparer.Ordinal);
    private readonly Dictionary<string, RequestTypeItem> _requestTypes = new(StringComparer.Ordinal);
    private readonly List<Guid> _models = [];
    private readonly List<Guid> _approvers = [];
    private readonly List<Guid> _itStaff = [];
    private readonly List<Guid> _intakeMembers = [];
    private readonly List<Guid> _fieldMembers = [];
    private readonly List<(Guid Id, Guid LocationId)> _employees = [];
    private readonly Dictionary<Guid, List<Guid>> _assignedAssets = [];
    private DateTimeOffset _setupCursor;
    private Guid _adminId;
    private Guid _fieldTeamId;

    private DateTimeOffset Cap => realNow - ActionMargin;

    public async Task<(string AdminEmail, bool AdminCreated)> RunAsync()
    {
        _setupCursor = _windowStart.AddDays(-SetupLeadDays);
        var admin = await EnsureAdminAsync();
        await SeedOrganizationAsync();
        await SeedPeopleAsync();
        await SeedCatalogAsync();
        await SeedAssetsAsync();
        await SeedArticlesAsync();
        await SeedTicketsAsync();
        return admin;
    }

    private async Task<(string AdminEmail, bool AdminCreated)> EnsureAdminAsync()
    {
        await using (var scope = scopes.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
            var existing = await db.Users.AsNoTracking()
                .Where(u => u.Role == UserRole.Admin && u.IsActive)
                .OrderBy(u => u.CreatedAt)
                .Select(u => new { u.Id, u.Email })
                .FirstOrDefaultAsync(cancellationToken);
            if (existing is not null)
            {
                _adminId = existing.Id;
                return (existing.Email, false);
            }
        }

        var email = Email(PilotSeedData.AdminEmailLocalPart);
        var result = await SetupAsync<SetupFirstAdminHandler, SetupFirstAdminResult>(
            null, (h, ct) => h.HandleAsync(new SetupFirstAdminCommand(email, PilotSeedData.AdminFirstName, PilotSeedData.AdminLastName), ct));
        _adminId = result.UserId;
        await SetPilotPasswordAsync(result.UserId, result.TemporaryPassword);
        return (email, true);
    }

    private async Task SeedOrganizationAsync()
    {
        for (var index = 0; index < PilotSeedVolumes.Scaled(PilotSeedVolumes.Approvers, scale); index++)
        {
            _approvers.Add(await CreateAccountAsync(PilotSeedData.ApproverEmailPrefix, index, UserRole.Admin, null, null));
        }

        for (var index = 0; index < PilotSeedData.Departments.Length; index++)
        {
            var name = PilotSeedData.Departments[index];
            var approver = _approvers[index % _approvers.Count];
            var department = await SetupAsync<CreateDepartmentHandler, DepartmentItem>(_adminId, (h, ct) => h.HandleAsync(new CreateDepartmentCommand(name, approver), ct));
            _departments[name] = department.Id;
        }

        foreach (var location in PilotSeedData.Locations)
        {
            Guid? parent = location.Parent is null ? null : _locations[location.Parent];
            var created = await SetupAsync<CreateLocationHandler, LocationItem>(_adminId, (h, ct) => h.HandleAsync(new CreateLocationCommand(location.Name, parent), ct));
            _locations[location.Name] = created.Id;
        }

        var intake = await SetupAsync<CreateTeamHandler, TeamDetail>(_adminId, (h, ct) => h.HandleAsync(new CreateTeamCommand(PilotSeedData.IntakeTeam), ct));
        await SetupAsync<UpdateTeamHandler, TeamDetail>(_adminId, (h, ct) => h.HandleAsync(new UpdateTeamCommand(intake.Id, intake.Version, intake.Name, IsDefaultIntake: true), ct));
        var field = await SetupAsync<CreateTeamHandler, TeamDetail>(_adminId, (h, ct) => h.HandleAsync(new CreateTeamCommand(PilotSeedData.FieldTeam), ct));
        _fieldTeamId = field.Id;

        for (var index = 0; index < PilotSeedVolumes.Scaled(PilotSeedVolumes.ItStaff, scale); index++)
        {
            var staff = await CreateAccountAsync(PilotSeedData.ItStaffEmailPrefix, index, UserRole.ItStaff, _departments[PilotSeedData.ItDepartment], LeafLocation());
            _itStaff.Add(staff);

            // The first one works in both teams, so each team has a member at any scale; then two of three join intake.
            var joinsIntake = index == 0 || index % 3 != 2;
            var joinsField = index == 0 || index % 3 == 2;
            if (joinsIntake)
            {
                await AddMemberAsync(intake.Id, staff);
                _intakeMembers.Add(staff);
            }

            if (joinsField)
            {
                await AddMemberAsync(field.Id, staff);
                _fieldMembers.Add(staff);
            }
        }
    }

    private async Task SeedPeopleAsync()
    {
        var businessDepartments = PilotSeedData.Departments.Where(d => d != PilotSeedData.ItDepartment).ToArray();
        for (var index = 0; index < PilotSeedVolumes.Scaled(PilotSeedVolumes.Employees, scale); index++)
        {
            var location = LeafLocation();
            var department = _departments[Pick(businessDepartments)];
            _employees.Add((await CreateAccountAsync(PilotSeedData.EmployeeEmailPrefix, index, UserRole.Employee, department, location), location));
        }
    }

    private async Task SeedCatalogAsync()
    {
        foreach (var category in PilotSeedData.Categories)
        {
            Guid? parent = category.Parent is null ? null : _categories[category.Parent];
            var created = await SetupAsync<CreateCategoryHandler, CategoryItem>(_adminId, (h, ct) => h.HandleAsync(new CreateCategoryCommand(category.Name, parent, category.Scope), ct));
            _categories[category.Name] = created.Id;
        }

        foreach (var type in PilotSeedData.RequestTypes)
        {
            _requestTypes[type.Name] = await SetupAsync<CreateRequestTypeHandler, RequestTypeItem>(
                _adminId, (h, ct) => h.HandleAsync(new CreateRequestTypeCommand(type.Name, type.RequiresApproval), ct));
        }

        var brands = new Dictionary<string, Guid>(StringComparer.Ordinal);
        foreach (var model in PilotSeedData.Models)
        {
            if (!brands.TryGetValue(model.Brand, out var brandId))
            {
                brandId = (await SetupAsync<CreateBrandHandler, BrandItem>(_adminId, (h, ct) => h.HandleAsync(new CreateBrandCommand(model.Brand), ct))).Id;
                brands[model.Brand] = brandId;
            }

            var created = await SetupAsync<CreateAssetModelHandler, AssetModelItem>(_adminId, (h, ct) => h.HandleAsync(new CreateAssetModelCommand(brandId, model.Type, model.Name), ct));
            _models.Add(created.Id);
        }
    }

    private async Task SeedAssetsAsync()
    {
        var holders = _employees.Select(e => e.Id).Concat(_itStaff).ToList();
        var setupDay = DateOnly.FromDateTime(_setupCursor.UtcDateTime);
        for (var index = 0; index < PilotSeedVolumes.Scaled(PilotSeedVolumes.Assets, scale); index++)
        {
            var purchased = setupDay.AddDays(-_random.Next(MinPurchaseAgeDays, MaxPurchaseAgeDays));
            var command = new CreateAssetCommand(
                Pick(_models),
                $"{PilotSeedData.SerialPrefix}{index + 1:D6}",
                LeafLocation(),
                _departments[Pick(PilotSeedData.Departments)],
                purchased.AddYears(Pick(WarrantyYears)),
                purchased,
                null);
            var asset = await SetupAsync<CreateAssetHandler, AssetItem>(_adminId, (h, ct) => h.HandleAsync(command, ct));

            var roll = _random.Next(Hundred);
            if (roll < AssignedPercent)
            {
                var holder = Pick(holders);
                await SetupAsync<AssetLifecycleHandler, AssetItem>(_adminId, (h, ct) => h.AssignAsync(new AssignAssetCommand(asset.Id, asset.Version, holder, null, null), ct));
                if (!_assignedAssets.TryGetValue(holder, out var held))
                {
                    _assignedAssets[holder] = held = [];
                }

                held.Add(asset.Id);
            }
            else if (roll < AssignedPercent + MaintenancePercent)
            {
                await SetupAsync<AssetLifecycleHandler, AssetItem>(_adminId, (h, ct) => h.StartMaintenanceAsync(new StartMaintenanceCommand(asset.Id, asset.Version, false), ct));
            }
            else if (roll < AssignedPercent + MaintenancePercent + LostPercent)
            {
                await SetupAsync<AssetLifecycleHandler, AssetItem>(_adminId, (h, ct) => h.MarkLostAsync(new MarkAssetLostCommand(asset.Id, asset.Version, PilotSeedData.LostReason), ct));
            }
            else if (roll < AssignedPercent + MaintenancePercent + LostPercent + RetiredPercent)
            {
                await SetupAsync<AssetLifecycleHandler, AssetItem>(_adminId, (h, ct) => h.RetireAsync(new RetireAssetCommand(asset.Id, asset.Version, PilotSeedData.RetireReason), ct));
            }
        }
    }

    private async Task SeedArticlesAsync()
    {
        var templates = PilotSeedData.Articles;
        for (var index = 0; index < PilotSeedVolumes.Scaled(PilotSeedVolumes.Articles, scale); index++)
        {
            var template = templates[index % templates.Length];
            var round = index / templates.Length;
            var title = round == 0 ? template.Title : $"{template.Title} ({round + 1})";
            var author = Pick(_itStaff);
            var at = Clamp(_windowStart.AddMinutes(_random.Next(PilotSeedVolumes.DaysBack * MinutesPerDay)));
            var command = new CreateArticleCommand(title, template.Summary, template.Body, _categories[template.Category], template.Visibility);
            var article = await ActAsync<CreateArticleHandler, ArticleDetail>(author, at, (h, ct) => h.HandleAsync(command, ct));
            await ActAsync<PublishArticleHandler, ArticleDetail>(author, Later(at, ArticlePublishDelay), (h, ct) => h.HandleAsync(new PublishArticleCommand(article.Id, article.Version), ct));
        }
    }

    private async Task SeedTicketsAsync()
    {
        var count = PilotSeedVolumes.Scaled(PilotSeedVolumes.Tickets, scale);
        var latest = realNow - CreationMargin;
        var firstDay = _windowStart.UtcDateTime.Date;
        var openedAt = Enumerable.Range(0, count)
            .Select(_ =>
            {
                var at = new DateTimeOffset(firstDay, TimeSpan.Zero)
                    .AddDays(_random.Next(PilotSeedVolumes.DaysBack) + 1)
                    .AddHours(OfficeOpensHourUtc)
                    .AddMinutes(_random.Next(OfficeMinutes));
                return at > latest ? latest : at;
            })
            .Order()
            .ToList();

        var plans = new List<PilotTicketPlan>(count);
        while (plans.Count < count)
        {
            var block = PlanBlock.ToArray();
            _random.Shuffle(block);
            plans.AddRange(block);
        }

        for (var index = 0; index < count; index++)
        {
            await SeedTicketAsync(openedAt[index], plans[index]);
        }
    }

    private async Task SeedTicketAsync(DateTimeOffset openedAt, PilotTicketPlan plan)
    {
        var (requester, location) = Pick(_employees);
        var at = openedAt;
        TicketDetail ticket;
        if (_random.Next(Hundred) < RequestPercent)
        {
            var template = Pick(PilotSeedData.Requests);
            var command = new CreateRequestCommand(template.Subject, template.Description, _requestTypes[template.RequestType].Id, _categories[template.Category], location);
            ticket = await ActAsync<CreateRequestHandler, TicketDetail>(requester, at, (h, ct) => h.HandleAsync(command, ct));
        }
        else
        {
            var template = Pick(PilotSeedData.Incidents);
            Guid? device = _random.Next(Hundred) < DeviceLinkPercent && _assignedAssets.TryGetValue(requester, out var held) ? Pick(held) : null;
            var command = new CreateIncidentCommand(template.Subject, template.Description, _categories[template.Category], location, device);
            ticket = await ActAsync<CreateIncidentHandler, TicketDetail>(requester, at, (h, ct) => h.HandleAsync(command, ct));
        }

        if (ticket.Approval is { Status: ApprovalStatus.Pending, ApproverId: { } approver } approval)
        {
            if (plan == PilotTicketPlan.OnHold && _random.Next(Hundred) < PendingApprovalPercent)
            {
                return;
            }

            at = Later(at, ApprovalDelay);
            var approve = plan != PilotTicketPlan.Cancelled;
            var decision = new DecideApprovalCommand(approval.Id, approval.Version, approve, approve ? null : PilotSeedData.RejectReason);
            ticket = await ActAsync<DecideApprovalHandler, TicketDetail>(approver, at, (h, ct) => h.HandleAsync(decision, ct));
            if (!approve)
            {
                return;
            }
        }

        if (plan == PilotTicketPlan.Open)
        {
            await MaybeCommentAsync(ticket.Number, requester, at, RequesterReplyPercent, PilotSeedData.RequesterReplies, Visibility.Public);
            return;
        }

        var worker = Pick(_intakeMembers);
        at = Later(at, ClaimDelay);
        ticket = await ActAsync<ClaimTicketHandler, TicketDetail>(worker, at, (h, ct) => h.HandleAsync(new ClaimTicketCommand(ticket.Number, ticket.Version), ct));
        if (_random.Next(Hundred) < PriorityChangePercent)
        {
            var command = new ChangePriorityCommand(ticket.Number, ticket.Version, Pick(Enum.GetValues<Level>()), Pick(Enum.GetValues<Level>()), PilotSeedData.PriorityReason);
            ticket = await ActAsync<ChangePriorityHandler, TicketDetail>(worker, at, (h, ct) => h.HandleAsync(command, ct));
        }

        if (plan == PilotTicketPlan.OpenAssigned)
        {
            return;
        }

        if (plan == PilotTicketPlan.Cancelled)
        {
            at = Later(at, StartDelay);
            await ActAsync<CancelTicketHandler, TicketDetail>(worker, at, (h, ct) => h.HandleAsync(new CancelTicketCommand(ticket.Number, ticket.Version, PilotSeedData.CancelReason), ct));
            return;
        }

        if (_random.Next(Hundred) < TransferPercent)
        {
            var assignee = Pick(_fieldMembers);
            var command = new TransferTicketCommand(ticket.Number, ticket.Version, _fieldTeamId, assignee, Pick(TransferReasons), null);
            at = Later(at, ClaimDelay);
            ticket = await ActAsync<TransferTicketHandler, TicketDetail>(worker, at, (h, ct) => h.HandleAsync(command, ct));
            worker = assignee;
        }

        at = Later(at, StartDelay);
        ticket = await ActAsync<StartTicketHandler, TicketDetail>(worker, at, (h, ct) => h.HandleAsync(new StartTicketCommand(ticket.Number, ticket.Version), ct));
        at = await MaybeCommentAsync(ticket.Number, worker, at, InternalNotePercent, PilotSeedData.InternalNotes, Visibility.Internal);

        switch (plan)
        {
            case PilotTicketPlan.InProgress:
                await MaybeCommentAsync(ticket.Number, requester, at, RequesterReplyPercent, PilotSeedData.RequesterReplies, Visibility.Public);
                return;
            case PilotTicketPlan.OnHold:
                await HoldAsync(ticket.Number, worker, Later(at, CommentDelay));
                return;
            default:
                await CompleteAsync(ticket, worker, requester, at);
                return;
        }
    }

    private async Task CompleteAsync(TicketDetail ticket, Guid worker, Guid requester, DateTimeOffset at)
    {
        if (_random.Next(Hundred) < HoldBeforeCompletePercent)
        {
            at = Later(at, CommentDelay);
            await HoldAsync(ticket.Number, worker, at);
            at = Later(at, HoldDuration);
            await CommentAsync(ticket.Number, requester, at, Pick(PilotSeedData.RequesterReplies), Visibility.Public);
            var version = await VersionAsync(ticket.Number, worker);
            await ActAsync<ResumeTicketHandler, TicketDetail>(worker, at, (h, ct) => h.HandleAsync(new ResumeTicketCommand(ticket.Number, version), ct));
        }

        at = await MaybeCommentAsync(ticket.Number, worker, at, WorkerReplyPercent, PilotSeedData.WorkerReplies, Visibility.Public);
        at = Later(at, WorkDuration);
        var current = await VersionAsync(ticket.Number, worker);
        var outcome = ticket.Kind == TicketKind.Incident ? TicketOutcome.Resolved : (TicketOutcome?)null;
        var rootCause = _random.Next(Hundred) < RootCausePercent ? PilotSeedData.RootCause : null;
        var command = new CompleteTicketCommand(ticket.Number, current, Pick(PilotSeedData.Resolutions), outcome, rootCause);
        await ActAsync<CompleteTicketHandler, TicketDetail>(worker, at, (h, ct) => h.HandleAsync(command, ct));
    }

    private async Task HoldAsync(string number, Guid worker, DateTimeOffset at)
    {
        var version = await VersionAsync(number, worker);
        var command = new HoldTicketCommand(number, version, Pick(HoldReasons), Pick(PilotSeedData.HoldNotes));
        await ActAsync<HoldTicketHandler, TicketDetail>(worker, at, (h, ct) => h.HandleAsync(command, ct));
    }

    private async Task<DateTimeOffset> MaybeCommentAsync(string number, Guid author, DateTimeOffset at, int percent, string[] bodies, Visibility visibility)
    {
        if (_random.Next(Hundred) >= percent)
        {
            return at;
        }

        at = Later(at, CommentDelay);
        await CommentAsync(number, author, at, Pick(bodies), visibility);
        return at;
    }

    private Task<CommentItem> CommentAsync(string number, Guid author, DateTimeOffset at, string body, Visibility visibility) =>
        ActAsync<AddCommentHandler, CommentItem>(author, at, (h, ct) => h.HandleAsync(new AddCommentCommand(number, body, visibility, []), ct));

    /// <summary>A comment touches the ticket (and so its version) without a version check; the next action reads it fresh.</summary>
    private async Task<uint> VersionAsync(string number, Guid viewer) =>
        (await ActAsync<GetTicketHandler, TicketDetail>(viewer, clock.GetUtcNow(), (h, ct) => h.HandleAsync(new GetTicketQuery(number), ct))).Version;

    private async Task<Guid> CreateAccountAsync(string emailPrefix, int index, UserRole role, Guid? departmentId, Guid? locationId)
    {
        var command = new CreateUserCommand(
            Pick(PilotSeedData.FirstNames), Pick(PilotSeedData.LastNames), Email($"{emailPrefix}{index + 1:D2}"), role, departmentId, locationId, CredentialDelivery.TemporaryPassword);
        var created = await SetupAsync<CreateUserHandler, TemporaryPasswordResult>(_adminId, (h, ct) => h.HandleAsync(command, ct));
        await SetPilotPasswordAsync(created.UserId, created.TemporaryPassword!);
        return created.UserId;
    }

    /// <summary>The account changes its own temporary password, as at first sign-in, to the one pilot password.</summary>
    private Task SetPilotPasswordAsync(Guid userId, string temporaryPassword) =>
        SetupAsync<ChangePasswordHandler, SessionUser>(userId, (h, ct) => h.HandleAsync(new ChangePasswordCommand(temporaryPassword, password), ct));

    private Task AddMemberAsync(Guid teamId, Guid userId) =>
        SetupAsync<AddTeamMemberHandler, TeamDetail>(_adminId, (h, ct) => h.HandleAsync(new AddTeamMemberCommand(teamId, userId), ct));

    private Task<TResult> SetupAsync<THandler, TResult>(Guid? userId, Func<THandler, CancellationToken, Task<TResult>> call)
        where THandler : notnull
    {
        _setupCursor += SetupStep;
        return ActAsync(userId, _setupCursor, call);
    }

    private async Task<TResult> ActAsync<THandler, TResult>(Guid? userId, DateTimeOffset at, Func<THandler, CancellationToken, Task<TResult>> call)
        where THandler : notnull
    {
        actor.UserId = userId;
        clock.Set(at);
        await using var scope = scopes.CreateAsyncScope();
        return await call(scope.ServiceProvider.GetRequiredService<THandler>(), cancellationToken);
    }

    private DateTimeOffset Later(DateTimeOffset at, (int Min, int Max) minutes) => Clamp(at.AddMinutes(_random.Next(minutes.Min, minutes.Max)));

    private DateTimeOffset Clamp(DateTimeOffset at) => at > Cap ? Cap : at;

    private Guid LeafLocation() => _locations[Pick(LeafLocations).Name];

    private T Pick<T>(IReadOnlyList<T> items) => items[_random.Next(items.Count)];

    private static string Email(string localPart) => $"{localPart}@{PilotSeed.EmailDomain}";
}
