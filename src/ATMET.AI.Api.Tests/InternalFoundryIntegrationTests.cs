using System.Net;
using ATMET.AI.Api.Tests.Fakes;
using Xunit;

namespace ATMET.AI.Api.Tests;

/// <summary>
/// Internal Foundry HTTP surface: auth + tenant header contract. API key comes from
/// <c>appsettings.Integration.json</c> merged in <see cref="AtmetApiFactory"/>.
/// </summary>
[Trait("Category", "Integration")]
public sealed class InternalFoundryIntegrationTests : IClassFixture<AtmetApiFactory>
{
    private readonly AtmetApiFactory _factory;

    public InternalFoundryIntegrationTests(AtmetApiFactory factory) => _factory = factory;

    private HttpClient CreateClientWithApiKey()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", IntegrationTestConfig.ApiKey);
        return client;
    }

    [Fact]
    public async Task GetCase_returns_400_when_entity_header_missing()
    {
        using var client = CreateClientWithApiKey();
        var response = await client.GetAsync(
            $"/api/v1/internal/foundry/cases/{FakeFoundryAgentReadService.ValidCaseId}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetCase_returns_404_when_entity_does_not_match_stub()
    {
        using var client = CreateClientWithApiKey();
        client.DefaultRequestHeaders.Add("X-Portal-Entity-Id", "99999999-9999-9999-9999-999999999999");

        var response = await client.GetAsync(
            $"/api/v1/internal/foundry/cases/{FakeFoundryAgentReadService.ValidCaseId}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetCase_returns_200_when_entity_and_case_match_stub()
    {
        using var client = CreateClientWithApiKey();
        client.DefaultRequestHeaders.Add("X-Portal-Entity-Id", FakeFoundryAgentReadService.ValidEntityId);

        var response = await client.GetAsync(
            $"/api/v1/internal/foundry/cases/{FakeFoundryAgentReadService.ValidCaseId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task GetCaseByReference_returns_200_for_valid_reference_and_entity()
    {
        using var client = CreateClientWithApiKey();
        client.DefaultRequestHeaders.Add("X-Portal-Entity-Id", FakeFoundryAgentReadService.ValidEntityId);

        var response = await client.GetAsync(
            $"/api/v1/internal/foundry/cases/by-reference/{FakeFoundryAgentReadService.ValidReference}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetService_returns_200_for_valid_service_and_entity()
    {
        using var client = CreateClientWithApiKey();
        client.DefaultRequestHeaders.Add("X-Portal-Entity-Id", FakeFoundryAgentReadService.ValidEntityId);

        var response = await client.GetAsync(
            $"/api/v1/internal/foundry/services/{FakeFoundryAgentReadService.ValidServiceId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetCase_returns_401_without_api_key()
    {
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Portal-Entity-Id", FakeFoundryAgentReadService.ValidEntityId);

        var response = await client.GetAsync(
            $"/api/v1/internal/foundry/cases/{FakeFoundryAgentReadService.ValidCaseId}");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
