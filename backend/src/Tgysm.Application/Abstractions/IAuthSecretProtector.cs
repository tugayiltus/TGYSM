namespace Tgysm.Application.Abstractions;

/// <summary>Protects login provider secrets (client secret, bind password) at rest with the shared key ring (ADR-0024, ADR-0030).</summary>
public interface IAuthSecretProtector
{
    string Protect(string secret);

    string Unprotect(string protectedSecret);
}
