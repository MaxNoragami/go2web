using System.Text;

namespace go2web.Http;

public record HttpResponse
{
    public string HttpVersion { get; init; } = string.Empty;
    public int StatusCode { get; init; }
    public string ReasonPhrase { get; init; } = string.Empty;
    
    public Dictionary<string, string> Headers { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    
    public byte[] BodyBytes { get; init; } = Array.Empty<byte>();

    public string BodyString => Encoding.UTF8.GetString(BodyBytes);

    public bool IsRedirect => StatusCode >= 300 && StatusCode < 400;

    public string? GetHeader(string name)
    {
        return Headers.TryGetValue(name, out var value) ? value : null;
    }
}
