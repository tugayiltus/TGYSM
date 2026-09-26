using System.Text.Json;
using FluentValidation.Results;
using Tgysm.Domain.Common;

namespace Tgysm.Api.Http;

/// <summary>Maps FluentValidation failures to camelCase field names and stable error codes.</summary>
internal static class ValidationErrorMapper
{
    private const char CodeSeparator = '.';

    private static readonly Dictionary<string, string> BuiltInValidatorCodes = new(StringComparer.Ordinal)
    {
        ["NotEmptyValidator"] = ErrorCodes.Required,
        ["NotNullValidator"] = ErrorCodes.Required,
        ["MaximumLengthValidator"] = ErrorCodes.TooLong,
        ["LengthValidator"] = ErrorCodes.TooLong,
        ["EmailValidator"] = ErrorCodes.InvalidEmail,
    };

    public static Dictionary<string, string[]> ToFieldCodes(IEnumerable<ValidationFailure> failures) =>
        failures
            .GroupBy(failure => JsonNamingPolicy.CamelCase.ConvertName(failure.PropertyName))
            .ToDictionary(group => group.Key, group => group.Select(MapCode).Distinct().ToArray());

    private static string MapCode(ValidationFailure failure) =>
        BuiltInValidatorCodes.TryGetValue(failure.ErrorCode, out var code) ? code
        : failure.ErrorCode.Contains(CodeSeparator) ? failure.ErrorCode
        : ErrorCodes.InvalidValue;
}
