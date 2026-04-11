using ATMET.AI.Api.Internal;
using Xunit;

namespace ATMET.AI.Api.Tests;

public class FoundryToolTenantHeaderTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void TryGetCanonicalEntityId_returns_false_when_missing(string? header)
    {
        var ok = FoundryToolTenantHeader.TryGetCanonicalEntityId(header, out var canonical);
        Assert.False(ok);
        Assert.Null(canonical);
    }

    [Theory]
    [InlineData("ent-1", "ent-1")]
    [InlineData("  uuid-here  ", "uuid-here")]
    public void TryGetCanonicalEntityId_returns_trimmed_id(string header, string expected)
    {
        var ok = FoundryToolTenantHeader.TryGetCanonicalEntityId(header, out var canonical);
        Assert.True(ok);
        Assert.Equal(expected, canonical);
    }
}
