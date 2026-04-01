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

    Task<HttpResponse> PostAsync(
        Uri uri,
        string body,
        string contentType = "application/x-www-form-urlencoded",
        int maxRedirects = 5,
        string acceptHeader = "text/html",
        string acceptLanguage = "*",
        Action<int, Uri>? onRedirect = null);
}