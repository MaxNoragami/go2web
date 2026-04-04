using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace go2web.Http;

[JsonSerializable(typeof(CacheEntry))]
public partial class CacheContext : JsonSerializerContext { }

public class HttpCache
{
    private readonly string _cacheDirectory;
    private readonly JsonSerializerOptions _options;
    private readonly CacheContext _context;

    public HttpCache()
    {
        string baseDataFolder = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        _cacheDirectory = Path.Combine(baseDataFolder, "go2web", "cache");
        if (!Directory.Exists(_cacheDirectory))
        {
            Directory.CreateDirectory(_cacheDirectory);
        }

        _options = new JsonSerializerOptions 
        { 
            PropertyNameCaseInsensitive = true,
            WriteIndented = false,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
        _context = new CacheContext(_options);
    }

    private string GetCacheKey(Uri uri, string acceptHeader, string acceptLanguage)
    {
        string raw = $"{uri.AbsoluteUri}|{acceptHeader}|{acceptLanguage}";
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private string GetFilePath(string cacheKey) => Path.Combine(_cacheDirectory, $"{cacheKey}.json");

    public CacheEntry? Get(Uri uri, string acceptHeader, string acceptLanguage)
    {
        string key = GetCacheKey(uri, acceptHeader, acceptLanguage);
        string path = GetFilePath(key);

        if (!File.Exists(path)) return null;

        try
        {
            string json = File.ReadAllText(path);
            return JsonSerializer.Deserialize(json, _context.CacheEntry);
        }
        catch
        {
            return null;
        }
    }

    public void Put(Uri uri, string acceptHeader, string acceptLanguage, HttpResponse response)
    {
        // Determine if response is cacheable
        string? cacheControl = response.GetHeader("Cache-Control");
        if (cacheControl != null && cacheControl.Contains("no-store", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        // Calculate expiration
        DateTimeOffset now = DateTimeOffset.UtcNow;
        DateTimeOffset? expiresAt = null;

        if (cacheControl != null)
        {
            var parts = cacheControl.Split(',').Select(p => p.Trim().ToLowerInvariant());
            foreach (var part in parts)
            {
                if (part.StartsWith("max-age="))
                {
                    if (int.TryParse(part.Substring(8), out int maxAge))
                    {
                        expiresAt = now.AddSeconds(maxAge);
                    }
                }
            }
        }

        var entry = new CacheEntry
        {
            Url = uri.AbsoluteUri,
            RequestHeaders = new Dictionary<string, string>
            {
                ["Accept"] = acceptHeader,
                ["Accept-Language"] = acceptLanguage
            },
            ResponseHeaders = response.Headers.ToDictionary(k => k.Key, v => v.Value, StringComparer.OrdinalIgnoreCase),
            StatusCode = response.StatusCode,
            CachedAt = now,
            ExpiresAt = expiresAt,
            ETag = response.GetHeader("ETag"),
            LastModified = response.GetHeader("Last-Modified"),
            BodyBase64 = Convert.ToBase64String(response.BodyBytes)
        };

        try
        {
            string key = GetCacheKey(uri, acceptHeader, acceptLanguage);
            string path = GetFilePath(key);
            string json = JsonSerializer.Serialize(entry, _context.CacheEntry);
            File.WriteAllText(path, json);
        }
        catch { }
    }

    public void UpdateExpiration(Uri uri, string acceptHeader, string acceptLanguage, HttpResponse newResponse)
    {
        var entry = Get(uri, acceptHeader, acceptLanguage);
        if (entry != null)
        {
            string? cacheControl = newResponse.GetHeader("Cache-Control");
            DateTimeOffset now = DateTimeOffset.UtcNow;
            DateTimeOffset? expiresAt = null;

            if (cacheControl != null)
            {
                var parts = cacheControl.Split(',').Select(p => p.Trim().ToLowerInvariant());
                foreach (var part in parts)
                {
                    if (part.StartsWith("max-age="))
                    {
                        if (int.TryParse(part.Substring(8), out int maxAge))
                        {
                            expiresAt = now.AddSeconds(maxAge);
                        }
                    }
                }
            }

            entry = entry with
            {
                ResponseHeaders = newResponse.Headers.ToDictionary(k => k.Key, v => v.Value, StringComparer.OrdinalIgnoreCase),
                ExpiresAt = expiresAt,
                CachedAt = now
            };

            try
            {
                string key = GetCacheKey(uri, acceptHeader, acceptLanguage);
                string path = GetFilePath(key);
                string json = JsonSerializer.Serialize(entry, _context.CacheEntry);
                File.WriteAllText(path, json);
            }
            catch { }
        }
    }
}