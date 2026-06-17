namespace FengDeskAI.Infrastructure.ExternalServices.Storage;

public sealed class SupabaseStorageOptions
{
    public const string SectionName = "SupabaseStorage";

    /// <summary>Project URL, vd "https://dgpvgxrrjxjnwnkljnjk.supabase.co".</summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>service_role key (hoặc storage key) — bí mật, để trong appsettings.Development / secrets.</summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>Tên bucket.</summary>
    public string Bucket { get; set; } = "Fengdesk_bucket";
}
