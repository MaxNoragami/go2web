using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace go2web.Http;

// Manages caching of HTTP responses based on request URI and headers, using a file-based cache with JSON serialization
public class HttpCache
{
    private readonly string _cacheDirectory;
    private readonly JsonSerializerOptions _options;
    private readonly CacheContext _context;

    // Constructor initializes the cache directory path and JSON serialization options
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

    // Generates a unique cache key based on the request URI and relevant headers (Accept and Accept-Language) by hashing them together using SHA256
    private string GetCacheKey(Uri uri, string acceptHeader, string acceptLanguage)
    {
        string raw = $"{uri.AbsoluteUri}|{acceptHeader}|{acceptLanguage}";
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    // Constructs the file path for a given cache key by combining the cache directory with the key and a .json extension
    private string GetFilePath(string cacheKey) => Path.Combine(_cacheDirectory, $"{cacheKey}.json");

    // Retrieves a cached response for the given URI and headers
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

    // Stores a response in the cache
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

        // If the Cache-Control header contains a max-age directive, use it to calculate the expiration time for the cache entry
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

    // Updates the expiration time of an existing cache entry based on a new HTTP response, typically used when revalidating a cached response with conditional headers
    public void UpdateExpiration(Uri uri, string acceptHeader, string acceptLanguage, HttpResponse newResponse)
    {
        // First, retrieve the existing cache entry for the given URI and headers
        var entry = Get(uri, acceptHeader, acceptLanguage);
        if (entry != null)
        {
            // If we have an existing cache entry, we want to update its expiration time based on the new response's Cache-Control header
            string? cacheControl = newResponse.GetHeader("Cache-Control");
            DateTimeOffset now = DateTimeOffset.UtcNow;
            DateTimeOffset? expiresAt = null;

            // If the new response's Cache-Control header contains a max-age directive, use it to calculate the new expiration time for the cache entry
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

            // Update the cache entry's response headers and expiration time based on the new response, then save the updated cache entry back to the file system
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