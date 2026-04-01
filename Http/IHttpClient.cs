namespace go2web.Http;

public interface IHttpClient
{
    Task<HttpResponse> GetAsync(
        Uri uri, 
        int maxRedirects = 5, 
        string acceptHeader = "text/html", 
        string acceptLanguage = "*", 
        Action<int, Uri>? onRedirect = null,
        string? ifNoneMatch = null,
        string? ifModifiedSince = null);
}