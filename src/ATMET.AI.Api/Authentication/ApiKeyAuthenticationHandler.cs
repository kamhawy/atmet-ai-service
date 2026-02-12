using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using System.Text.Encodings.Web;

namespace ATMET.AI.Api.Authentication;

/// <summary>
/// Authenticates requests using an API key passed in the configured HTTP header.
/// </summary>
public class ApiKeyAuthenticationHandler : AuthenticationHandler<ApiKeyAuthenticationOptions>
{
    private const string ApiKeyScheme = ApiKeyAuthenticationOptions.DefaultScheme;

    public ApiKeyAuthenticationHandler(
        IOptionsMonitor<ApiKeyAuthenticationOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var headerName = Options.HeaderName;
        if (string.IsNullOrEmpty(headerName))
            headerName = "X-Api-Key";

        if (!Request.Headers.TryGetValue(headerName, out var keyValues) || keyValues.Count == 0)
        {
            return Task.FromResult(AuthenticateResult.Fail("Missing API key."));
        }

        var providedKey = keyValues.ToString().Trim();
        if (string.IsNullOrEmpty(providedKey))
        {
            return Task.FromResult(AuthenticateResult.Fail("Missing API key."));
        }

        var validKeys = Options.ValidKeys;
        if (validKeys is null || validKeys.Count == 0)
        {
            Logger.LogWarning("No API keys configured. Rejecting request.");
            return Task.FromResult(AuthenticateResult.Fail("API key authentication is not configured."));
        }

        if (!validKeys.Contains(providedKey, StringComparer.Ordinal))
        {
            return Task.FromResult(AuthenticateResult.Fail("Invalid API key."));
        }

        var identity = new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, "api-key"),
                new Claim(ClaimTypes.Name, "ApiKey")
            ],
            ApiKeyScheme);

        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, ApiKeyScheme);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
