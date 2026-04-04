namespace go2web.Http;

public record CacheEntry
{
    public string Url { get; init; } = string.Empty;
    public Dictionary<string, string> RequestHeaders { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, string> ResponseHeaders { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    public int StatusCode { get; init; }
    public DateTimeOffset CachedAt { get; init; }
    public DateTimeOffset? ExpiresAt { get; init; }
    public string? ETag { get; init; }
    public string? LastModified { get; init; }
    public string BodyBase64 { get; init; } = string.Empty;
}
