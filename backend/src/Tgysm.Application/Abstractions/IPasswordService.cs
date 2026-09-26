namespace Tgysm.Application.Abstractions;

public interface IPasswordService
{
    string Hash(string password);

    bool Verify(string passwordHash, string password);

    /// <summary>Hash of a random secret; verifying against it costs the same time as a real check (login timing).</summary>
    string DummyHash { get; }
}
