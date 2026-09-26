using Tgysm.Domain.Common;

namespace Tgysm.Domain.Users;

/// <remarks>
/// <see cref="Entity.Version"/> is application-managed here: sign-in bookkeeping and a user's own password change
/// update this row often, and must not make an admin's open edit stale.
/// </remarks>
public sealed class User : Entity
{
    public const int NameMaxLength = 100;
    public const int EmailMaxLength = 254;
    public const int SearchNameMaxLength = (NameMaxLength * 2) + EmailMaxLength + 2;
    public const int SecurityStampMaxLength = 64;
    public const int MaxFailedSignInAttempts = 5;
    public static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

    private const string SecurityStampFormat = "N";

    private User()
    {
    }

    public string FirstName { get; private set; } = string.Empty;
    public string LastName { get; private set; } = string.Empty;
    public string Email { get; private set; } = string.Empty;
    public string NormalizedEmail { get; private set; } = string.Empty;
    public string SearchName { get; private set; } = string.Empty;
    public UserRole Role { get; private set; }
    public Guid? DepartmentId { get; private set; }
    public Guid? LocationId { get; private set; }
    public bool IsActive { get; private set; }
    public bool MustChangePassword { get; private set; }
    public string PasswordHash { get; private set; } = string.Empty;
    public string SecurityStamp { get; private set; } = string.Empty;
    public int AccessFailedCount { get; private set; }
    public DateTimeOffset? LockoutEnd { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    /// <summary>Emails compare with invariant lower case; Turkish casing would turn "I" into "ı".</summary>
    public static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();

    public static User Create(
        string firstName,
        string lastName,
        string email,
        UserRole role,
        Guid? departmentId,
        Guid? locationId,
        string temporaryPasswordHash,
        DateTimeOffset now)
    {
        var user = new User { Role = role, IsActive = true, CreatedAt = now };
        user.SetProfile(firstName, lastName, email, departmentId, locationId, now);
        user.SetTemporaryPassword(temporaryPasswordHash, now);
        return user;
    }

    public void UpdateProfile(string firstName, string lastName, string email, Guid? departmentId, Guid? locationId, DateTimeOffset now)
    {
        SetProfile(firstName, lastName, email, departmentId, locationId, now);
        BumpVersion();
    }

    public void ChangeRole(UserRole role, DateTimeOffset now)
    {
        if (Role == role)
        {
            return;
        }

        Role = role;
        TouchAndRotateStamp(now);
        BumpVersion();
    }

    public void Deactivate(DateTimeOffset now)
    {
        if (!IsActive)
        {
            return;
        }

        IsActive = false;
        TouchAndRotateStamp(now);
        BumpVersion();
    }

    public void Reactivate(DateTimeOffset now)
    {
        if (IsActive)
        {
            return;
        }

        IsActive = true;
        TouchAndRotateStamp(now);
        BumpVersion();
    }

    public void SetTemporaryPassword(string passwordHash, DateTimeOffset now)
    {
        PasswordHash = passwordHash;
        MustChangePassword = true;
        ClearLockout();
        TouchAndRotateStamp(now);
    }

    public void ChangePassword(string passwordHash, DateTimeOffset now)
    {
        PasswordHash = passwordHash;
        MustChangePassword = false;
        TouchAndRotateStamp(now);
    }

    public bool IsLockedOut(DateTimeOffset now) => LockoutEnd is { } end && end > now;

    public bool CanSignIn(DateTimeOffset now) => IsActive && !IsLockedOut(now);

    /// <summary>Counts a failed sign-in. Returns true when this failure started a lockout.</summary>
    public bool RegisterFailedSignIn(DateTimeOffset now)
    {
        AccessFailedCount++;
        UpdatedAt = now;
        if (AccessFailedCount < MaxFailedSignInAttempts)
        {
            return false;
        }

        AccessFailedCount = 0;
        LockoutEnd = now + LockoutDuration;
        return true;
    }

    public void RegisterSuccessfulSignIn(DateTimeOffset now)
    {
        ClearLockout();
        UpdatedAt = now;
    }

    private void SetProfile(string firstName, string lastName, string email, Guid? departmentId, Guid? locationId, DateTimeOffset now)
    {
        FirstName = firstName.Trim();
        LastName = lastName.Trim();
        Email = email.Trim();
        NormalizedEmail = NormalizeEmail(email);
        SearchName = TurkishText.ToSearchKey($"{FirstName} {LastName} {NormalizedEmail}");
        DepartmentId = departmentId;
        LocationId = locationId;
        UpdatedAt = now;
    }

    private void ClearLockout()
    {
        AccessFailedCount = 0;
        LockoutEnd = null;
    }

    private void TouchAndRotateStamp(DateTimeOffset now)
    {
        UpdatedAt = now;
        SecurityStamp = Guid.NewGuid().ToString(SecurityStampFormat);
    }
}
