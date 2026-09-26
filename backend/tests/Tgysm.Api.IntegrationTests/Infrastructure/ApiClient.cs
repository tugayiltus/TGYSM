using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc.Testing;
using Tgysm.Domain.Users;

namespace Tgysm.Api.IntegrationTests.Infrastructure;

/// <summary>HTTPS test client with cookie jar and antiforgery handling, like the browser SPA.</summary>
public sealed class ApiClient
{
    public const string CsrfHeader = "X-CSRF-TOKEN";
    public const string CsrfPath = "/api/v1/auth/csrf";
    public const string LoginPath = "/api/v1/auth/login";
    public const string MePath = "/api/v1/auth/me";
    public const string LogoutPath = "/api/v1/auth/logout";
    private const string CodeProperty = "code";
    private const string ErrorsProperty = "errors";
    private const string TokenProperty = "token";

    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) },
    };

    private static readonly Uri HttpsBase = new("https://localhost");
    private string? _csrfToken;

    private ApiClient(HttpClient http) => Http = http;

    public HttpClient Http { get; }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public static ApiClient Create(ApiFactory factory) =>
        new(factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = HttpsBase,
            HandleCookies = true,
            AllowAutoRedirect = false,
        }));

    public static async Task<(ApiClient Client, User User)> LoginAsNewUserAsync(ApiFactory factory, UserRole role, string email)
    {
        var user = await TestUsers.CreateAsync(factory, role, email);
        var client = Create(factory);
        await client.LoginAsync(email);
        return (client, user);
    }

    public async Task RefreshCsrfAsync()
    {
        var body = await Http.GetFromJsonAsync<JsonElement>(CsrfPath, Ct);
        _csrfToken = body.GetProperty(TokenProperty).GetString();
    }

    public Task<HttpResponseMessage> GetAsync(string url) => Http.GetAsync(url, Ct);

    public Task<HttpResponseMessage> PostAsync(string url, object? body = null, bool withCsrf = true) =>
        SendAsync(HttpMethod.Post, url, body, withCsrf);

    public Task<HttpResponseMessage> PutAsync(string url, object body) => SendAsync(HttpMethod.Put, url, body, withCsrf: true);

    public Task<HttpResponseMessage> DeleteAsync(string url) => SendAsync(HttpMethod.Delete, url, body: null, withCsrf: true);

    public async Task<HttpResponseMessage> SendAsync(HttpMethod method, string url, object? body, bool withCsrf)
    {
        if (withCsrf && _csrfToken is null)
        {
            await RefreshCsrfAsync();
        }

        using var request = new HttpRequestMessage(method, url);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body, options: Json);
        }

        if (withCsrf)
        {
            request.Headers.Add(CsrfHeader, _csrfToken);
        }

        return await Http.SendAsync(request, Ct);
    }

    /// <summary>Logs in and refreshes the antiforgery token, which is bound to the signed-in user.</summary>
    public async Task LoginAsync(string email, string password = TestUsers.DefaultPassword)
    {
        using var response = await PostAsync(LoginPath, new { email, password });
        response.EnsureSuccessStatusCode();
        await RefreshCsrfAsync();
    }

    public async Task<T> ReadJsonAsync<T>(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<T>(Json, Ct))!;

    public static async Task<string?> ReadCodeAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        return body.TryGetProperty(CodeProperty, out var code) ? code.GetString() : null;
    }

    public static async Task<IReadOnlyDictionary<string, string[]>> ReadFieldCodesAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        return body.TryGetProperty(ErrorsProperty, out var errors)
            ? errors.Deserialize<Dictionary<string, string[]>>(Json)!
            : new Dictionary<string, string[]>();
    }
}
