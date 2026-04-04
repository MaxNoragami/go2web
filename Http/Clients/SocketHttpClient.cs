using System.Net.Sockets;
using System.Net;
using System.Net.Security;
using System.Text;

namespace go2web.Http.Clients;

public class SocketHttpClient : IHttpClient
{
    public async Task<HttpResponse> GetAsync(
        Uri uri, 
        int maxRedirects = 5, 
        string acceptHeader = "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8", 
        string acceptLanguage = "*", 
        Action<int, 
        Uri>? onRedirect = null, 
        string? ifNoneMatch = null, 
        string? ifModifiedSince = null)
    {
        Uri currentUri = uri;
        int redirectsCount = 0;

        while (true)
        {
            bool isHttps = currentUri.Scheme == "https";
            if (currentUri.Scheme != "http" && currentUri.Scheme != "https")
            {
                throw new NotSupportedException($"Only HTTP and HTTPS schemes are supported currently. Provided: {currentUri.Scheme}");
            }

            int port = currentUri.Port > 0 ? currentUri.Port : (isHttps ? 443 : 80);

            var hostEntry = await Dns.GetHostEntryAsync(currentUri.Host);
            var ipAddress = hostEntry.AddressList.FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork) 
                            ?? hostEntry.AddressList[0];

            using var socket = new Socket(ipAddress.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            await socket.ConnectAsync(ipAddress, port);

            using var networkStream = new NetworkStream(socket, ownsSocket: true);
            Stream stream = networkStream;

            if (isHttps)
            {
                var sslStream = new SslStream(
                    networkStream, 
                    false, 
                    new RemoteCertificateValidationCallback((sender, certificate, chain, sslPolicyErrors) => true)
                );
                await sslStream.AuthenticateAsClientAsync(currentUri.Host);
                stream = sslStream;
            }

            // Manually construct the raw HTTP GET request string
            var pathAndQuery = currentUri.PathAndQuery;
            if (string.IsNullOrEmpty(pathAndQuery))
            {
                pathAndQuery = "/";
            }

            var requestBuilder = new StringBuilder();
            requestBuilder.Append($"GET {pathAndQuery} HTTP/1.1\r\n");
            requestBuilder.Append($"Host: {currentUri.Host}\r\n");
            requestBuilder.Append("Connection: close\r\n");
            requestBuilder.Append("User-Agent: go2web-client/1.0\r\n");
            requestBuilder.Append($"Accept: {acceptHeader}\r\n");
            requestBuilder.Append($"Accept-Language: {acceptLanguage}\r\n");

            if (!string.IsNullOrEmpty(ifNoneMatch))
                requestBuilder.Append($"If-None-Match: {ifNoneMatch}\r\n");
            if (!string.IsNullOrEmpty(ifModifiedSince))
                requestBuilder.Append($"If-Modified-Since: {ifModifiedSince}\r\n");

            requestBuilder.Append("\r\n"); // End of headers

            byte[] requestBytes = Encoding.ASCII.GetBytes(requestBuilder.ToString());
            await stream.WriteAsync(requestBytes, 0, requestBytes.Length);

            // Read and parse the response
            var response = await ParseResponseAsync(stream);

            if (response.StatusCode == 301 || response.StatusCode == 302 || 
                response.StatusCode == 303 || response.StatusCode == 307 || 
                response.StatusCode == 308)
            {
                if (redirectsCount >= maxRedirects)
                {
                    throw new Exception($"Too many redirects (exceeded maximum of {maxRedirects})");
                }

                var location = response.GetHeader("Location");
                if (!string.IsNullOrEmpty(location))
                {
                    currentUri = new Uri(currentUri, location);
                    redirectsCount++;
                    onRedirect?.Invoke(response.StatusCode, currentUri);
                    continue;
                }
            }

            return response;
        }
    }

    private async Task<HttpResponse> ParseResponseAsync(Stream stream)
    {
        var response = new HttpResponse();

        // Helper to read a line up to \r\n
        async Task<string> ReadLineAsync()
        {
            var bytes = new List<byte>();
            while (true)
            {
                var buffer = new byte[1];
                int read = await stream.ReadAsync(buffer, 0, 1);
                if (read == 0) break; // EOF

                bytes.Add(buffer[0]);
                if (bytes.Count >= 2 && bytes[^2] == '\r' && bytes[^1] == '\n')
                {
                    break;
                }
            }

            // Return string without \r\n
            return Encoding.ASCII.GetString(bytes.ToArray(), 0, Math.Max(0, bytes.Count - 2));
        }

        // Read Status Line
        string statusLine = await ReadLineAsync();
        if (string.IsNullOrEmpty(statusLine))
            throw new Exception("Empty response from server.");

        var statusParts = statusLine.Split(' ', 3);
        if (statusParts.Length >= 2)
        {
            response.HttpVersion = statusParts[0];
            if (int.TryParse(statusParts[1], out int statusCode))
                response.StatusCode = statusCode;
            if (statusParts.Length >= 3)
                response.ReasonPhrase = statusParts[2];
        }

        // Read Headers
        while (true)
        {
            string headerLine = await ReadLineAsync();
            if (string.IsNullOrEmpty(headerLine))
            {
                // End of headers (\r\n\r\n)
                break;
            }

            int colonIndex = headerLine.IndexOf(':');
            if (colonIndex > 0)
            {
                string name = headerLine.Substring(0, colonIndex).Trim();
                string value = headerLine.Substring(colonIndex + 1).Trim();
                response.Headers[name] = value;
            }
        }

        // Read Body
        bool isChunked = response.GetHeader("Transfer-Encoding")?.Equals("chunked", StringComparison.OrdinalIgnoreCase) == true;
        string? contentLengthHeader = response.GetHeader("Content-Length");
        bool hasContentLength = int.TryParse(contentLengthHeader, out int contentLength);

        using var bodyStream = new MemoryStream();

        if (isChunked)
        {
            // Handle Transfer-Encoding: chunked
            while (true)
            {
                string chunkHexSizeLine = await ReadLineAsync();
                
                // Some chunk sizes might have trailing extensions like "1A; extension=value", we only need the hex part
                string hexSize = chunkHexSizeLine.Split(';')[0].Trim();
                
                if (string.IsNullOrEmpty(hexSize)) 
                    continue;

                int chunkSize = Convert.ToInt32(hexSize, 16);
                if (chunkSize == 0)
                {
                    // End of chunks
                    await ReadLineAsync(); // Read trailing \r\n after the last 0-sized chunk
                    break;
                }

                // Read chunk data
                byte[] chunkData = new byte[chunkSize];
                int bytesRead = 0;
                while (bytesRead < chunkSize)
                {
                    int read = await stream.ReadAsync(chunkData, bytesRead, chunkSize - bytesRead);
                    if (read == 0) throw new Exception("Unexpected end of stream while reading chunk.");
                    bytesRead += read;
                }
                bodyStream.Write(chunkData, 0, chunkSize);

                // Read the \r\n after chunk data
                await ReadLineAsync();
            }
        }
        else if (hasContentLength)
        {
            // Read exactly Content-Length bytes
            byte[] buffer = new byte[8192];
            int totalRead = 0;
            while (totalRead < contentLength)
            {
                int toRead = Math.Min(buffer.Length, contentLength - totalRead);
                int read = await stream.ReadAsync(buffer, 0, toRead);
                if (read == 0) break; // Unexpected EOF
                
                bodyStream.Write(buffer, 0, read);
                totalRead += read;
            }
        }
        else
        {
            // Read until Connection: close drops the connection (EOF)
            byte[] buffer = new byte[8192];
            while (true)
            {
                int read = await stream.ReadAsync(buffer, 0, buffer.Length);
                if (read == 0) break; // Connection dropped/EOF
                bodyStream.Write(buffer, 0, read);
            }
        }

        response.BodyBytes = bodyStream.ToArray();

        return response;
    }
}
