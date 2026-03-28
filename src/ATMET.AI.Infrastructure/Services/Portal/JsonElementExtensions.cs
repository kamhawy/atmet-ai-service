using System.Text.Json;

namespace ATMET.AI.Infrastructure.Services.Portal;

/// <summary>
/// Helper extensions for safely reading nullable properties from JsonElement.
/// </summary>
internal static class JsonElementExtensions
{
    /// <summary>
    /// Get a nullable string property, returning null if missing or null.
    /// </summary>
    public static string? GetProp(this JsonElement el, string name)
    {
        if (el.TryGetProperty(name, out var prop) && prop.ValueKind == JsonValueKind.String)
            return prop.GetString();
        return null;
    }

    /// <summary>
    /// Get a nullable JsonElement property, returning null if missing or null.
    /// </summary>
    public static JsonElement? GetJsonProp(this JsonElement el, string name)
    {
        if (el.TryGetProperty(name, out var prop) &&
            prop.ValueKind != JsonValueKind.Null &&
            prop.ValueKind != JsonValueKind.Undefined)
            return prop;
        return null;
    }

    /// <summary>
    /// Get a nullable int property.
    /// </summary>
    public static int? GetIntProp(this JsonElement el, string name)
    {
        if (el.TryGetProperty(name, out var prop) && prop.ValueKind == JsonValueKind.Number)
            return prop.GetInt32();
        return null;
    }

    /// <summary>
    /// Get a nullable DateTimeOffset property.
    /// </summary>
    public static DateTimeOffset? GetDateProp(this JsonElement el, string name)
    {
        if (el.TryGetProperty(name, out var prop) && prop.ValueKind == JsonValueKind.String)
        {
            var str = prop.GetString();
            if (str != null && DateTimeOffset.TryParse(str, out var dt))
                return dt;
        }
        return null;
    }
}
