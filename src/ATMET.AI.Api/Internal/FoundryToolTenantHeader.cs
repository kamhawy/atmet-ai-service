using System.Diagnostics.CodeAnalysis;

namespace ATMET.AI.Api.Internal;

/// <summary>
/// Validates <c>X-Portal-Entity-Id</c> for internal Foundry tool HTTP routes (shared with portal multi-tenant rules).
/// </summary>
public static class FoundryToolTenantHeader
{
    /// <summary>
    /// Returns <c>true</c> and a trimmed entity id when the header is present and non-whitespace.
    /// </summary>
    public static bool TryGetCanonicalEntityId([NotNullWhen(true)] string? entityId, [NotNullWhen(true)] out string? canonical)
    {
        if (string.IsNullOrWhiteSpace(entityId))
        {
            canonical = null;
            return false;
        }

        canonical = entityId.Trim();
        return true;
    }
}
