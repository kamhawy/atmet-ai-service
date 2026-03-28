namespace ATMET.AI.Infrastructure.Configuration;

/// <summary>
/// Configuration options for Supabase database connectivity.
/// </summary>
public class SupabaseOptions
{
    public const string SectionName = "Supabase";

    /// <summary>
    /// Supabase project URL (e.g. https://xxxx.supabase.co).
    /// </summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>
    /// Service Role Key — bypasses Row Level Security. Use server-side only.
    /// </summary>
    public string ServiceRoleKey { get; set; } = string.Empty;

    /// <summary>
    /// Storage bucket name for case documents.
    /// </summary>
    public string DocumentsBucket { get; set; } = "application-documents";
}
