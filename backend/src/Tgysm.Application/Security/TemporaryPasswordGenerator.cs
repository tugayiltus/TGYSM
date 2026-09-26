using System.Security.Cryptography;
using Tgysm.Application.Abstractions;

namespace Tgysm.Application.Security;

/// <summary>Cryptographically random temporary passwords that satisfy <see cref="PasswordRules"/>.</summary>
public sealed class TemporaryPasswordGenerator : ITemporaryPasswordGenerator
{
    public const int Length = 16;

    // Ambiguous characters (0/O, 1/l/I) are left out; admins read these passwords to users.
    private const string Letters = "abcdefghjkmnpqrstuvwxyzABCDEFGHJKLMNPQRSTUVWXYZ";
    private const string Digits = "23456789";
    private const string Alphabet = Letters + Digits;
    private const int GuaranteedCharacters = 2;

    public string Generate()
    {
        var characters = new char[Length];
        characters[0] = Letters[RandomNumberGenerator.GetInt32(Letters.Length)];
        characters[1] = Digits[RandomNumberGenerator.GetInt32(Digits.Length)];
        RandomNumberGenerator.GetItems(Alphabet.AsSpan(), characters.AsSpan(GuaranteedCharacters));
        RandomNumberGenerator.Shuffle(characters.AsSpan());
        return new string(characters);
    }
}
