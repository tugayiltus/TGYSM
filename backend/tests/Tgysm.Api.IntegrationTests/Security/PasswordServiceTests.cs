using Tgysm.Infrastructure.Security;

namespace Tgysm.Api.IntegrationTests.Security;

public sealed class PasswordServiceTests
{
    private const string Password = "Correct-horse-1";

    private readonly PasswordService _service = new();

    [Fact]
    public void Hash_verifies_only_the_original_password()
    {
        var hash = _service.Hash(Password);

        Assert.NotEqual(Password, hash);
        Assert.True(_service.Verify(hash, Password));
        Assert.False(_service.Verify(hash, Password + "x"));
    }

    [Fact]
    public void Dummy_hash_rejects_any_password() =>
        Assert.False(_service.Verify(_service.DummyHash, Password));
}
