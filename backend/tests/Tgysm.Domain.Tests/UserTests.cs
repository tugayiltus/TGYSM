using Tgysm.Domain.Users;

namespace Tgysm.Domain.Tests;

public sealed class UserTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 26, 9, 0, 0, TimeSpan.Zero);

    private static User NewUser(UserRole role = UserRole.Employee) =>
        User.Create("İlker", "Işık", "  Ilker.Isik@Example.COM ", role, null, null, "temp-hash", Now);

    [Fact]
    public void Create_sets_temporary_password_state_and_normalized_fields()
    {
        var user = NewUser();

        Assert.True(user.IsActive);
        Assert.True(user.MustChangePassword);
        Assert.Equal("Ilker.Isik@Example.COM", user.Email);
        Assert.Equal("ilker.isik@example.com", user.NormalizedEmail);
        Assert.Equal("ilker işik ilker.isik@example.com", user.SearchName);
        Assert.NotEmpty(user.SecurityStamp);
        Assert.Equal(Now, user.CreatedAt);
    }

    [Fact]
    public void Five_failed_sign_ins_lock_the_account_for_fifteen_minutes()
    {
        var user = NewUser();

        for (var attempt = 1; attempt < User.MaxFailedSignInAttempts; attempt++)
        {
            Assert.False(user.RegisterFailedSignIn(Now));
        }

        Assert.True(user.RegisterFailedSignIn(Now));
        Assert.True(user.IsLockedOut(Now));
        Assert.False(user.CanSignIn(Now.AddMinutes(14)));
        Assert.True(user.CanSignIn(Now + User.LockoutDuration));
    }

    [Fact]
    public void Successful_sign_in_resets_failed_attempts()
    {
        var user = NewUser();
        user.RegisterFailedSignIn(Now);

        user.RegisterSuccessfulSignIn(Now);

        Assert.Equal(0, user.AccessFailedCount);
        Assert.Null(user.LockoutEnd);
    }

    [Fact]
    public void ChangeRole_rotates_stamp_only_when_role_changes()
    {
        var user = NewUser();
        var stamp = user.SecurityStamp;

        user.ChangeRole(UserRole.Employee, Now);
        Assert.Equal(stamp, user.SecurityStamp);

        user.ChangeRole(UserRole.ItStaff, Now);
        Assert.Equal(UserRole.ItStaff, user.Role);
        Assert.NotEqual(stamp, user.SecurityStamp);
    }

    [Fact]
    public void Deactivate_rotates_stamp_and_blocks_sign_in()
    {
        var user = NewUser();
        var stamp = user.SecurityStamp;

        user.Deactivate(Now);

        Assert.False(user.IsActive);
        Assert.False(user.CanSignIn(Now));
        Assert.NotEqual(stamp, user.SecurityStamp);
    }

    [Fact]
    public void ChangePassword_clears_must_change_flag_and_rotates_stamp()
    {
        var user = NewUser();
        var stamp = user.SecurityStamp;

        user.ChangePassword("new-hash", Now);

        Assert.False(user.MustChangePassword);
        Assert.Equal("new-hash", user.PasswordHash);
        Assert.NotEqual(stamp, user.SecurityStamp);
    }

    [Fact]
    public void SetTemporaryPassword_requires_change_and_clears_lockout()
    {
        var user = NewUser();
        user.ChangePassword("new-hash", Now);
        for (var attempt = 0; attempt < User.MaxFailedSignInAttempts; attempt++)
        {
            user.RegisterFailedSignIn(Now);
        }

        user.SetTemporaryPassword("temp-2", Now);

        Assert.True(user.MustChangePassword);
        Assert.False(user.IsLockedOut(Now));
    }

    [Theory]
    [InlineData(UserRole.Employee, "employee")]
    [InlineData(UserRole.ItStaff, "it_staff")]
    [InlineData(UserRole.Admin, "admin")]
    public void Role_names_round_trip(UserRole role, string name)
    {
        Assert.Equal(name, UserRoleNames.ToName(role));
        Assert.True(UserRoleNames.TryParse(name, out var parsed));
        Assert.Equal(role, parsed);
    }

    [Fact]
    public void Unknown_role_name_does_not_parse() =>
        Assert.False(UserRoleNames.TryParse("superuser", out _));

    [Fact]
    public void Admin_edits_bump_version_but_sign_in_and_own_password_change_do_not()
    {
        var user = NewUser();
        var created = user.Version;

        user.RegisterFailedSignIn(Now);
        user.RegisterSuccessfulSignIn(Now);
        user.ChangePassword("own-hash", Now);
        Assert.Equal(created, user.Version);

        user.UpdateProfile("İlker", "Işık", "ilker@example.com", null, null, Now);
        user.ChangeRole(UserRole.ItStaff, Now);
        user.Deactivate(Now);
        user.Reactivate(Now);
        Assert.Equal(created + 4, user.Version);
    }
}
