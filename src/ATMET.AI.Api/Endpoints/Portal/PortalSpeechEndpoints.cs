using ATMET.AI.Core.Models.Portal;
using ATMET.AI.Infrastructure.Configuration;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace ATMET.AI.Api.Endpoints.Portal;

/// <summary>
/// Azure Speech token issuance for **client-side** <c>microsoft-cognitiveservices-speech-sdk</c> (STT/TTS).
/// </summary>
public static class PortalSpeechEndpoints
{
    public static void MapEndpoints(RouteGroupBuilder group)
    {
        var speech = group.MapGroup("/portal/speech")
            .WithTags("Portal - Speech");

        speech.MapGet("/token", GetSpeechToken)
            .WithName("GetPortalSpeechToken")
            .WithSummary("Issue a short-lived Speech SDK authorization token")
            .WithDescription("""
                Returns a **short-lived token** (~10 minutes) and **region** for configuring the JavaScript Speech SDK in the browser (`SpeechConfig.fromAuthorizationToken`).

                **Flow:** the MUBASHIR chat UI requests this before starting microphone capture; the token is **not** a substitute for portal identity — it only authorizes audio against your Azure Speech resource.

                **Headers:** `X-Portal-User-Id`, `X-Portal-Entity-Id` (same as other portal mutations).

                **Server configuration:** requires **`AzureSpeech:Region`** and **`AzureSpeech:Key`**. The token URL defaults to **`https://{region}.api.cognitive.microsoft.com/sts/v1.0/issueToken`**; override with **`AzureSpeech:Endpoint`** for sovereign or custom hosts. If misconfigured, the endpoint returns **503**.
                """)
            .Produces<SpeechTokenResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status502BadGateway)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable)
            .RequireAuthorization("ApiReader");
    }

    private static async Task<IResult> GetSpeechToken(
        [FromHeader(Name = "X-Portal-User-Id")] string? userId,
        [FromHeader(Name = "X-Portal-Entity-Id")] string? entityId,
        [FromServices] IHttpClientFactory httpClientFactory,
        [FromServices] IOptions<AzureSpeechOptions> options,
        [FromServices] ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger("ATMET.AI.Api.Endpoints.PortalSpeech");

        if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(entityId))
        {
            return Results.BadRequest(new
            {
                error = "X-Portal-User-Id and X-Portal-Entity-Id headers are required."
            });
        }

        var opt = options.Value;
        var tokenUrl = ResolveIssueTokenUrl(opt);
        if (string.IsNullOrWhiteSpace(opt.Region) || string.IsNullOrWhiteSpace(opt.Key) || string.IsNullOrWhiteSpace(tokenUrl))
        {
            logger.LogWarning("Azure Speech token requested but AzureSpeech (Region, Key, or token URL) is not configured");
            return Results.Problem(
                detail: "Azure Speech is not configured on the server.",
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        if (!Uri.TryCreate(tokenUrl, UriKind.Absolute, out var tokenUri))
        {
            logger.LogWarning("AzureSpeech token URL is invalid: {TokenUrl}", tokenUrl);
            return Results.Problem(
                detail: "Azure Speech endpoint configuration is invalid.",
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        var client = httpClientFactory.CreateClient("AzureSpeech");
        var request = new HttpRequestMessage(HttpMethod.Post, tokenUri);
        request.Headers.TryAddWithoutValidation("Ocp-Apim-Subscription-Key", opt.Key);

        try
        {
            using var response = await client.SendAsync(request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                logger.LogWarning(
                    "Azure Speech token issuance failed: {Status} {Body}",
                    (int)response.StatusCode,
                    body);
                return Results.Problem(
                    detail: "Speech token issuance failed.",
                    statusCode: StatusCodes.Status502BadGateway);
            }

            var token = await response.Content.ReadAsStringAsync(cancellationToken);
            var trimmed = token.Trim();
            if (string.IsNullOrEmpty(trimmed))
            {
                logger.LogWarning("Azure Speech token response body was empty");
                return Results.Problem(
                    detail: "Speech token issuance returned an empty token.",
                    statusCode: StatusCodes.Status502BadGateway);
            }

            return Results.Ok(new SpeechTokenResponse(trimmed, opt.Region.Trim(), 10));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to reach Azure Speech token endpoint");
            return Results.Problem(
                detail: "Could not reach the speech token service.",
                statusCode: StatusCodes.Status502BadGateway);
        }
    }

    /// <summary>
    /// Uses <see cref="AzureSpeechOptions.Endpoint"/> when set; otherwise builds the standard Cognitive Services issueToken URL from <see cref="AzureSpeechOptions.Region"/>.
    /// </summary>
    private static string? ResolveIssueTokenUrl(AzureSpeechOptions opt)
    {
        if (!string.IsNullOrWhiteSpace(opt.Endpoint))
            return opt.Endpoint.Trim();

        if (string.IsNullOrWhiteSpace(opt.Region))
            return null;

        return $"https://{opt.Region.Trim()}.api.cognitive.microsoft.com/sts/v1.0/issueToken";
    }
}
