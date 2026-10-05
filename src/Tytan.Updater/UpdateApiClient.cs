using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Tytan.Updater;

public sealed partial class UpdateApiClient : IDisposable
{
    private readonly HttpClient http;
    private readonly Uri baseUri;
    private readonly AuthenticationHeaderValue authentication;

    // When supplied, the handler is owned by this client and must not follow redirects.
    public UpdateApiClient(Uri baseUri, string username, string password, HttpMessageHandler? handler = null,
        TimeSpan? timeout = null)
    {
        ArgumentNullException.ThrowIfNull(baseUri);
        if (!baseUri.IsAbsoluteUri || baseUri.Scheme != Uri.UriSchemeHttps ||
            baseUri.Query.Length != 0 || baseUri.Fragment.Length != 0 || baseUri.UserInfo.Length != 0)
            throw new ArgumentException("The base URL must use HTTPS and contain no credentials or query parameters.", nameof(baseUri));
        ArgumentException.ThrowIfNullOrWhiteSpace(username);
        ArgumentNullException.ThrowIfNull(password);
        if (username.Contains(':') || username.Any(char.IsControl) || password.Any(char.IsControl))
            throw new ArgumentException("Invalid credentials.");
        var requestTimeout = timeout ?? TimeSpan.FromMinutes(2);
        if (requestTimeout <= TimeSpan.Zero || requestTimeout > TimeSpan.FromDays(1))
            throw new ArgumentOutOfRangeException(nameof(timeout));
        this.baseUri = new Uri(baseUri.AbsoluteUri.TrimEnd('/') + "/");
        authentication = new("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes(username + ":" + password)));
        http = new HttpClient(handler ?? new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = requestTimeout };
    }

    public async Task<IReadOnlyList<FileEntry>> ListAsync(string? clientFolder = null, CancellationToken cancellationToken = default)
    {
        if (clientFolder is not null) PathRules.ValidateSegment(clientFolder, "Client folder");
        var endpoint = "api.php" + (clientFolder is null ? "" : "?dir=" + Uri.EscapeDataString(clientFolder));
        using var request = CreateRequest(endpoint);
        using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        CheckResponse(response);
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(http.Timeout);
        try
        {
            var entries = await JsonSerializer.DeserializeAsync<List<FileEntry>>(stream, cancellationToken: timeout.Token);
            if (entries is null || entries.Any(e => e is null || string.IsNullOrEmpty(e.Name) ||
                e.Type is not ("file" or "folder") || string.IsNullOrEmpty(e.Path) || e.Size is < 0))
                throw new InvalidDataException("The server listing contains incomplete or invalid data.");
            return entries;
        }
        catch (JsonException)
        {
            throw new InvalidDataException("The server did not return a valid JSON listing.");
        }
    }

    private HttpRequestMessage CreateRequest(string endpoint)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, new Uri(baseUri, endpoint));
        request.Headers.Authorization = authentication;
        return request;
    }

    private static void CheckResponse(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode) return;
        var message = response.StatusCode switch
        {
            HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => "Access denied by the server.",
            HttpStatusCode.NotFound => "Folder, file or endpoint not found.",
            _ when (int)response.StatusCode is >= 300 and < 400 => "The server returned a redirect; check the base URL.",
            _ => $"The server returned HTTP error {(int)response.StatusCode}."
        };
        throw new HttpRequestException(message, null, response.StatusCode);
    }

    public void Dispose() => http.Dispose();
}
