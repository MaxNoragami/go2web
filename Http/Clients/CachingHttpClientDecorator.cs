namespace go2web.Http.Clients;

// A decorator for IHttpClient that adds caching capabilities using an HttpCache instance
public class CachingHttpClientDecorator : IHttpClient
{
    private readonly IHttpClient _innerClient;
    private readonly HttpCache _cache;

    public CachingHttpClientDecorator(IHttpClient innerClient)
    {
        _innerClient = innerClient;
        _cache = new HttpCache();
    }

    // The main method to perform an HTTP GET request with caching logic
    public async Task<HttpResponse> GetAsync(
        Uri uri, 
        int maxRedirects = 5, 
        string acceptHeader = "text/html", 
        string acceptLanguage = "*", 
        Action<int, Uri>? onRedirect = null,
        string? ifNoneMatch = null,
        string? ifModifiedSince = null)
    {
        // First, check if we have a cached response for this URI and headers
        var cached = _cache.Get(uri, acceptHeader, acceptLanguage);
        bool isCacheExpired = true;
        bool sendConditional = false;
        
        // If we have a cached response, determine if it's still fresh or if we should send conditional headers to revalidate it
        if (cached != null)
        {
            // Check if the cached response has an Expires header that is still in the future
            if (cached.ExpiresAt.HasValue && cached.ExpiresAt.Value > DateTimeOffset.UtcNow)
            {
                isCacheExpired = false;
            }
            else if (!string.IsNullOrEmpty(cached.ETag) || !string.IsNullOrEmpty(cached.LastModified))
            {
                sendConditional = true;
            }
            
            // If the cache is not expired and we don't need to send conditional headers, return the cached response immediately
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

        // If we need to send conditional headers for revalidation, include the ETag and Last-Modified values from the cache in the request
        string? conditionalETag = sendConditional && cached != null ? cached.ETag : ifNoneMatch;
        string? conditionalLastModified = sendConditional && cached != null ? cached.LastModified : ifModifiedSince;

        // Perform the actual HTTP GET request using the inner client, passing along the conditional headers if needed
        var response = await _innerClient.GetAsync(uri, maxRedirects, acceptHeader, acceptLanguage, onRedirect, conditionalETag, conditionalLastModified);

        // If we sent conditional headers and received a 304 Not Modified response, it means our cached response is still valid. Update its expiration and return it
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
            // If we got a fresh 200 OK response, cache it for future use
            _cache.Put(uri, acceptHeader, acceptLanguage, response);
        }

        return response;
    }
}