using System;
using Xunit;

namespace ATMET.AI.Api.Tests;

public class IntegrationTestConfigTests
{
    [Fact]
    public void ApiKey_loads_from_appsettings_Integration()
    {
        var key = IntegrationTestConfig.ApiKey;
        Assert.False(string.IsNullOrWhiteSpace(key));
        Assert.StartsWith("atmet-integration-test-key", key, StringComparison.Ordinal);
    }
}
