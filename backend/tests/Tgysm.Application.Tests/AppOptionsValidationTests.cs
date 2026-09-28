using Tgysm.Application.Common;

namespace Tgysm.Application.Tests;

/// <summary>
/// <see cref="AppOptionsValidation.IsValidPublicBaseUrl"/> — the rule both hosts enforce via
/// <c>ValidateOnStart</c> (review fix round 1) so a blank/relative <c>App:PublicBaseUrl</c> fails fast instead of
/// silently rendering a relative <c>ticketUrl</c> in email.
/// </summary>
public sealed class AppOptionsValidationTests
{
    [Theory]
    [InlineData("https://tgysm.example.com")]
    [InlineData("http://localhost:5173")]
    public void Accepts_an_absolute_http_or_https_url(string url) =>
        Assert.True(AppOptionsValidation.IsValidPublicBaseUrl(url));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("/tickets")]
    [InlineData("tgysm.example.com")]
    [InlineData("ftp://tgysm.example.com")]
    public void Rejects_blank_relative_or_non_http_urls(string? url) =>
        Assert.False(AppOptionsValidation.IsValidPublicBaseUrl(url));
}
