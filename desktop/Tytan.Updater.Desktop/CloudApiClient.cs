using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Tytan.Updater.Desktop;

internal sealed record RemoteEntry(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("size")] long? Size,
    [property: JsonPropertyName("modified")] string Modified,
    [property: JsonPropertyName("path")] string Path);

internal sealed class CloudApiClient : IDisposable
{
    internal const string DefaultBaseUrl = "https://tytan.poznan.pl/SQLupdate/";
    private readonly Uri baseUri;
    private readonly HttpClient http;
    private readonly bool ownsHttp;

    public CloudApiClient(Uri? baseUri = null, HttpClient? http = null)
    {
        this.baseUri = baseUri ?? new Uri(DefaultBaseUrl);
        if (!this.baseUri.IsAbsoluteUri || this.baseUri.Scheme != "https" ||
            this.baseUri.UserInfo != "" || this.baseUri.Query != "" || this.baseUri.Fragment != "" ||
            !this.baseUri.AbsolutePath.EndsWith('/'))
        {
            throw new ArgumentException("The API base URL must be HTTPS, without credentials or a query, and end with '/'.");
        }

        ownsHttp = http is null;
        // Never forward the shared credentials through an automatic redirect.
        this.http = http ?? new HttpClient(new HttpClientHandler { AllowAutoRedirect = false })
        {
            Timeout = TimeSpan.FromSeconds(30)
        };
    }

    public async Task<IReadOnlyList<RemoteEntry>> ListAsync(
        string clientFolder, string username, string password, CancellationToken cancellationToken)
    {
        // Cover both response headers and streamed JSON with one overall deadline.
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(30));
        CancellationToken requestToken = deadline.Token;
        if (!InstallationFileReader.ValidName(clientFolder))
        {
            throw new InvalidDataException("A valid client folder must be loaded first.");
        }

        if (string.IsNullOrWhiteSpace(username) || password.Length == 0 || username.Contains(':') ||
            username.Any(char.IsControl) || password.Any(char.IsControl))
        {
            throw new InvalidDataException("Enter a valid username and password.");
        }

        // Always request the loaded client's folder, never the root client list.
        var endpoint = new Uri(baseUri, "api.php?dir=" + Uri.EscapeDataString(clientFolder));
        using var request = new HttpRequestMessage(HttpMethod.Get, endpoint);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic",
            Convert.ToBase64String(Encoding.UTF8.GetBytes(username + ":" + password)));

        using HttpResponseMessage response = await http.SendAsync(
            request, HttpCompletionOption.ResponseHeadersRead, requestToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            string message = response.StatusCode switch
            {
                HttpStatusCode.Unauthorized => "The server rejected the credentials (HTTP 401).",
                HttpStatusCode.Forbidden => "The server denied access (HTTP 403).",
                HttpStatusCode.NotFound => "The client's cloud folder or API was not found (HTTP 404).",
                HttpStatusCode.InternalServerError => "The API could not complete the request (HTTP 500).",
                HttpStatusCode.ServiceUnavailable => "The API is unavailable or not configured (HTTP 503).",
                _ when (int)response.StatusCode is >= 300 and < 400 =>
                    $"The API returned a redirect (HTTP {(int)response.StatusCode}); redirects are not followed.",
                _ => $"The API request failed (HTTP {(int)response.StatusCode})."
            };
            throw new HttpRequestException(message, null, response.StatusCode);
        }

        if (response.Content.Headers.ContentType?.MediaType != "application/json")
        {
            throw new InvalidDataException("The API returned an unexpected response instead of JSON.");
        }

        try
        {
            // Limit listing size so a malformed response cannot grow without bounds.
            const int maximumBytes = 8 * 1024 * 1024;
            using Stream stream = await response.Content.ReadAsStreamAsync(requestToken).ConfigureAwait(false);
            using var body = new MemoryStream();
            byte[] buffer = new byte[8192];
            int count;
            while ((count = await stream.ReadAsync(buffer, requestToken).ConfigureAwait(false)) > 0)
            {
                if (body.Length + count > maximumBytes)
                {
                    throw new InvalidDataException("The API listing exceeds the allowed response size.");
                }
                body.Write(buffer, 0, count);
            }

            var entries = JsonSerializer.Deserialize<List<RemoteEntry?>>(body.ToArray(),
                new JsonSerializerOptions { MaxDepth = 16 });
            if (entries is null)
            {
                throw new InvalidDataException("The API must return an array of entries.");
            }

            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var result = new List<RemoteEntry>();
            foreach (RemoteEntry? entry in entries)
            {
                if (entry is null || !InstallationFileReader.ValidName(entry.Name) ||
                    entry.Path != clientFolder + "/" + entry.Name || !names.Add(entry.Name) ||
                    !DateTime.TryParseExact(entry.Modified, "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture,
                        DateTimeStyles.None, out _))
                {
                    throw new InvalidDataException("The API returned invalid metadata or an entry outside the loaded client folder.");
                }

                bool validFile = entry.Type == "file" && entry.Size is >= 0 &&
                    entry.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase);
                bool validFolder = entry.Type == "folder" && entry.Size is null;
                if (!validFile && !validFolder)
                {
                    throw new InvalidDataException("The API returned an invalid folder or ZIP entry.");
                }
                result.Add(entry);
            }

            requestToken.ThrowIfCancellationRequested();
            return result.AsReadOnly();
        }
        catch (JsonException error)
        {
            throw new InvalidDataException("The API returned malformed listing JSON.", error);
        }
    }

    public void Dispose()
    {
        if (ownsHttp)
        {
            http.Dispose();
        }
    }
}
