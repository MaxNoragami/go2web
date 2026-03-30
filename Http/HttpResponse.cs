namespace go2web.Http;

public class HttpResponse
{
    public string HttpVersion { get; set; } = string.Empty;
    public int StatusCode { get; set; }
    public string ReasonPhrase { get; set; } = string.Empty;
    
    public Dictionary<string, string> Headers { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    
    public byte[] BodyBytes { get; set; } = Array.Empty<byte>();

    public string BodyString => System.Text.Encoding.UTF8.GetString(BodyBytes);

    public bool IsRedirect => StatusCode >= 300 && StatusCode < 400;

    public string? GetHeader(string name)
    {
        return Headers.TryGetValue(name, out var value) ? value : null;
    }
}
