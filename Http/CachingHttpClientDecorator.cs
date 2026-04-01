namespace go2web.Http;

public class CachingHttpClientDecorator : IHttpClient
{
    private readonly IHttpClient _innerClient;
    private readonly HttpCache _cache;

    public CachingHttpClientDecorator(IHttpClient innerClient)
    {
        _innerClient = innerClient;
        _cache = new HttpCache();
    }

    public async Task<HttpResponse> GetAsync(
        Uri uri, 
        int maxRedirects = 5, 
        string acceptHeader = "text/html", 
        string acceptLanguage = "*", 
        Action<int, Uri>? onRedirect = null,
        string? ifNoneMatch = null,
        string? ifModifiedSince = null)
    {
        var cached = _cache.Get(uri, acceptHeader, acceptLanguage);
        bool isCacheExpired = true;
        bool sendConditional = false;
        
        if (cached != null)
        {
            if (cached.ExpiresAt.HasValue && cached.ExpiresAt.Value > DateTimeOffset.UtcNow)
            {
                isCacheExpired = false;
            }
            else if (!string.IsNullOrEmpty(cached.ETag) || !string.IsNullOrEmpty(cached.LastModified))
            {
                sendConditional = true;
            }
            
            if (!isCacheExpired && !sendConditional) 
            {
                return new HttpResponse
                {
                    HttpVersion = "HTTP/1.1",
                    StatusCode = cached.StatusCode,
                    ReasonPhrase = "OK (Cached)",
                    Headers = cached.ResponseHeaders,
                    BodyBytes = Convert.FromBase64String(cached.BodyBase64)
                };
            }
        }

        string? conditionalETag = sendConditional && cached != null ? cached.ETag : ifNoneMatch;
        string? conditionalLastModified = sendConditional && cached != null ? cached.LastModified : ifModifiedSince;

        var response = await _innerClient.GetAsync(uri, maxRedirects, acceptHeader, acceptLanguage, onRedirect, conditionalETag, conditionalLastModified);

        if (sendConditional && response.StatusCode == 304 && cached != null)
        {
            _cache.UpdateExpiration(uri, acceptHeader, acceptLanguage, response);
            
            var mergedHeaders = new Dictionary<string, string>(cached.ResponseHeaders, StringComparer.OrdinalIgnoreCase);
            foreach(var h in response.Headers) mergedHeaders[h.Key] = h.Value;

            return new HttpResponse
            {
                HttpVersion = "HTTP/1.1",
                StatusCode = cached.StatusCode,
                ReasonPhrase = "OK (Cached Revalidated)",
                Headers = mergedHeaders,
                BodyBytes = Convert.FromBase64String(cached.BodyBase64)
            };
        }
        else if (response.StatusCode == 200)
        {
            _cache.Put(uri, acceptHeader, acceptLanguage, response);
        }

        return response;
    }

    public async Task<HttpResponse> PostAsync(
        Uri uri,
        string body,
        string contentType = "application/x-www-form-urlencoded",
        int maxRedirects = 5,
        string acceptHeader = "text/html",
        string acceptLanguage = "*",
        Action<int, Uri>? onRedirect = null)
    {
        // Don't cache POST requests, just pass them through
        return await _innerClient.PostAsync(uri, body, contentType, maxRedirects, acceptHeader, acceptLanguage, onRedirect);
    }
}