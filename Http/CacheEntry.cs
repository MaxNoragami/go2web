using System.Text.Json.Serialization;

namespace go2web.Http;

public class CacheEntry
{
    public string Url { get; set; } = string.Empty;
    public Dictionary<string, string> RequestHeaders { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, string> ResponseHeaders { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public int StatusCode { get; set; }
    public DateTimeOffset CachedAt { get; set; }
    public DateTimeOffset? ExpiresAt { get; set; }
    public string? ETag { get; set; }
    public string? LastModified { get; set; }
    public string BodyBase64 { get; set; } = string.Empty;
}
