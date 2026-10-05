# Tytan Updater - Technical Code Walkthrough

Date: October 5, 2026.

Audience: developers learning the project or connecting the module to TytanSQL.

This guide explains the current implementation, with excerpts copied from its C# source files. Excerpts may omit surrounding declarations and are not all standalone programs. The integration example uses placeholder variables provided by Tytan.

## 1. What the application does

Tytan supplies the client folder, product, installed version, and local destination. The module queries the HTTPS API, chooses the highest available version of that product, and downloads its ZIP only when it is newer. It returns the local path so Tytan can apply the package.

The module does not discover the installed version automatically, install files, execute SQL, or change Tytan's installed-version record. There is a command-line interface, not a graphical window.

The live request to the documented listing endpoint returned HTTP 404 on October 5. Local tests validate the documented contract, not the live server or installation inside Tytan.

## 2. Repository map

| File or project | Responsibility |
| --- | --- |
| `src/Tytan.Updater/Models.cs` | Request, response, remote file, and package data |
| `src/Tytan.Updater/UpdateService.cs` | Coordinate the complete operation |
| `src/Tytan.Updater/UpdateApiClient.cs` | HTTPS, authentication, JSON listing, and HTTP errors |
| `src/Tytan.Updater/PackageSelector.cs` | Select a product's newest valid ZIP |
| `src/Tytan.Updater/PackageVersion.cs` | Parse and compare three numeric version components |
| `src/Tytan.Updater/PackageDownload.cs` | Download, validate, and publish a local package |
| `src/Tytan.Updater/PathRules.cs` | Validate names and package paths |
| `src/Tytan.Updater.Cli/Program.cs` | Commands, configuration, cancellation, and console output |
| `src/Tytan.Updater.Cli/DemoHandler.cs` | In-memory server used by the demo |
| `tests/Tytan.Updater.Tests/Program.cs` | Local test runner and test cases |
| `tests/Tytan.Updater.Tests/StubHandler.cs` | Inject simulated HTTP responses |
| `tests/Tytan.Updater.Tests/InterruptedStream.cs` | Simulate a failing download stream |

The library and CLI currently target `net9.0`. There are no external application dependencies. Compatibility with Tytan's actual project remains to be checked during integration.

## 3. Complete execution flow

```text
Tytan or CLI creates UpdateRequest
    |
UpdateService.CheckAndDownloadAsync
    |
Validate inputs and parse installed version
    |
UpdateApiClient.ListAsync(clientFolder)
    |
PackageSelector.SelectNewest(files, product)
    |
Compare selected version with installed version
    +-- Missing / equal / older -> UpdateResult.NoUpdate
    +-- Newer
         |
       UpdateApiClient.DownloadAsync
         |
       Temporary file -> size check -> ZIP read -> final filename
         |
       UpdateResult.Downloaded with LocalPath
         |
       Tytan's existing installation workflow
```

Example: `Faktury` version `008.000.042` is installed and `Faktury_008.000.043.zip` exists remotely. The service downloads the ZIP and returns its path. If `008.000.043` or a higher version is already installed, it does not download.

## 4. Models.cs - the data contract

A `record` groups related values into an object with value-based equality. These records are data containers; they do not contact the server themselves.

Source: `src/Tytan.Updater/Models.cs`

```csharp
using System.Text.Json.Serialization;

namespace Tytan.Updater;

public sealed record FileEntry(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("size")] long? Size,
    [property: JsonPropertyName("modified")] string? Modified,
    [property: JsonPropertyName("path")] string Path);

public sealed record UpdateRequest(string ClientFolder, string Product, string InstalledVersion, string DestinationDirectory);
public sealed record UpdatePackage(FileEntry File, PackageVersion Version);
public enum UpdateStatus { NoUpdate, Downloaded, Error, Cancelled }
public sealed record UpdateResult(UpdateStatus Status, string Product, string InstalledVersion,
    string? AvailableVersion = null, string? LocalPath = null, string? Message = null);
```

`FileEntry` represents a JSON listing entry. `JsonPropertyName("name")` maps the lowercase JSON field to the C# property `Name`. This avoids depending on case-insensitive deserialization. `long?` and `string?` allow null values.

`UpdateRequest` is the input from Tytan. `UpdatePackage` pairs a remote file with its parsed version. `UpdateResult` is the output; optional fields remain null when unavailable.

| Status | Meaning | LocalPath |
| --- | --- | --- |
| `NoUpdate` | No package exists or its version is not newer | Null |
| `Downloaded` | Complete package saved and validated | Final local path |
| `Error` | Invalid input, HTTP failure, timeout, or file failure | Null |
| `Cancelled` | Caller requested cancellation | Null |

`InstalledVersion` describes the version supplied by Tytan. `AvailableVersion` describes the selected remote version; it does not indicate successful installation.

## 5. UpdateService.cs - coordination

This is the main method to call from Tytan. Its constructor receives an `UpdateApiClient` rather than creating one, so the caller controls configuration and client lifetime.

Source: `src/Tytan.Updater/UpdateService.cs`

```csharp
            var files = await api.ListAsync(request.ClientFolder, cancellationToken);
            var package = PackageSelector.SelectNewest(files, request.Product);
            available = package?.Version.ToString();
            if (package is null || package.Version.CompareTo(installed) <= 0)
                return new(UpdateStatus.NoUpdate, request.Product, request.InstalledVersion, available,
                    Message: package is null ? "No package is available for this product." : "No newer version is available.");
            var path = await api.DownloadAsync(package, request.ClientFolder, request.DestinationDirectory, cancellationToken);
            return new(UpdateStatus.Downloaded, request.Product, request.InstalledVersion, available, path,
                "Package downloaded; Tytan can proceed with installation.");
        }
```

Before this excerpt, the method checks cancellation, validates the folder and product, checks the destination, and parses the installed version.

`await api.ListAsync(...)` obtains the listing. `SelectNewest(...)` chooses the product's package. `package?.Version.ToString()` returns null if there is no package.

`CompareTo(installed) <= 0` means the remote version is equal or older. In that case, the method returns without calling the download endpoint or creating the destination folder. Otherwise it downloads and returns the complete path.

### Error handling

Source: `src/Tytan.Updater/UpdateService.cs`

```csharp
        catch (OperationCanceledException)
        {
            return new(cancellationToken.IsCancellationRequested ? UpdateStatus.Cancelled : UpdateStatus.Error,
                request.Product, request.InstalledVersion, available,
                Message: cancellationToken.IsCancellationRequested ? "Operation cancelled." : "Request timed out.");
        }
        catch (HttpRequestException e)
        {
            return new(UpdateStatus.Error, request.Product, request.InstalledVersion, available,
                Message: e.StatusCode is { } status ? $"HTTP error {(int)status}; check access and the server path." : "Could not connect to the server.");
        }
        catch (Exception e) when (e is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return new(UpdateStatus.Error, request.Product, request.InstalledVersion, available, Message: e.Message);
        }
```

A cancellation exception becomes `Cancelled` if the caller's token was cancelled; otherwise it becomes `Error` with a timeout message. HTTP exceptions become `Error` with the status code when available. Expected input and file exceptions also become error results.

A null `UpdateRequest` is rejected before the try block and throws `ArgumentNullException`. The method does not catch every possible exception: unexpected programming failures can still propagate.

Application-defined messages are in English. The last catch uses `e.Message`, so messages originating from operating-system or framework exceptions may depend on the runtime environment.

## 6. UpdateApiClient.cs - HTTPS and BasicAuth

The constructor validates an absolute HTTPS base URL without embedded credentials, query parameters, or a fragment. It validates credentials, ensures a trailing slash, and creates its own HttpClient.

### Authentication and client setup

Source: `src/Tytan.Updater/UpdateApiClient.cs`

```csharp
        var requestTimeout = timeout ?? TimeSpan.FromMinutes(2);
        if (requestTimeout <= TimeSpan.Zero || requestTimeout > TimeSpan.FromDays(1))
            throw new ArgumentOutOfRangeException(nameof(timeout));
        this.baseUri = new Uri(baseUri.AbsoluteUri.TrimEnd('/') + "/");
        authentication = new("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes(username + ":" + password)));
        http = new HttpClient(handler ?? new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = requestTimeout };
```

BasicAuth encodes `username:password` as Base64 and places it in the Authorization header. Base64 is encoding, not encryption; HTTPS protects transport. Actual credentials must come from external configuration, not source code.

The default timeout is two minutes. Automatic redirects are disabled. This avoids silently changing the configured endpoint; redirects are reported as errors. An injected handler is owned and disposed by this client and must preserve the no-redirect behavior.

### Listing and deserialization

Source: `src/Tytan.Updater/UpdateApiClient.cs`

```csharp
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
```

With no folder, the method queries `api.php`. With `Barcin_Wodbar`, it queries `api.php?dir=Barcin_Wodbar`. `Uri.EscapeDataString` encodes spaces and other query characters.

`ResponseHeadersRead` avoids buffering the entire body before returning the response. The listing is deserialized from its stream into `List<FileEntry>`. Invalid JSON is converted into `InvalidDataException`. Missing required fields, unsupported entry types, and negative sizes are also rejected. An empty array is a valid listing.

The request/header phase uses HttpClient's timeout. The subsequent JSON body read has its own linked timeout. The listing therefore does not have one single two-minute deadline spanning both phases.

### Creating requests and checking responses

Source: `src/Tytan.Updater/UpdateApiClient.cs`

```csharp
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
```

Every request receives the BasicAuth header. A successful HTTP response continues; failures such as 401, 403, 404, redirects, and server errors throw an HTTP exception. The code does not treat an error response as an empty listing.

`Dispose()` releases HttpClient and its handler. Reuse the API client for the lifetime of the Tytan component, then dispose it; avoid creating a new client for every individual file.

## 7. PackageVersion.cs - numeric version parsing

A version consists of `Major`, `Minor`, and `Patch`, compared from left to right. Leading zeros affect display, not numeric comparison.

### Parsing

Source: `src/Tytan.Updater/PackageVersion.cs`

```csharp
    public static bool TryParse(string? value, out PackageVersion version)
    {
        version = default;
        if (value is null) return false;
        var parts = value.Split('.');
        if (parts.Length != 3) return false;
        var numbers = new int[3];
        for (var i = 0; i < parts.Length; i++)
        {
            if (parts[i].Length == 0 || !parts[i].All(c => c is >= '0' and <= '9') ||
                !int.TryParse(parts[i], NumberStyles.None, CultureInfo.InvariantCulture, out numbers[i])) return false;
        }
        version = new(numbers[0], numbers[1], numbers[2]);
        return true;
    }
```

`Split('.')` separates the three components. Each must contain ASCII digits and fit in a nonnegative C# int. Empty components, signs, whitespace, letters, additional components, and numeric overflow are rejected.

The accepted format requires three components but not exactly three digits per component: both `8.0.43` and `008.000.043` are valid and represent the same value.

### Comparing and formatting

Source: `src/Tytan.Updater/PackageVersion.cs`

```csharp
    public int CompareTo(PackageVersion other)
    {
        var result = Major.CompareTo(other.Major);
        if (result != 0) return result;
        result = Minor.CompareTo(other.Minor);
        return result != 0 ? result : Patch.CompareTo(other.Patch);
    }

    public override string ToString() => string.Create(CultureInfo.InvariantCulture, $"{Major:D3}.{Minor:D3}.{Patch:D3}");
```

`CompareTo` returns a positive number for a newer version, zero for an equal version, and a negative number for an older version. The first differing component decides the comparison. For example, `008.010.000` is newer than `008.009.999`.

`D3` formats each component with at least three digits, so `(8, 0, 43)` becomes `008.000.043`. It does not truncate components longer than three digits.

## 8. PackageSelector.cs - choosing the correct package

Source: `src/Tytan.Updater/PackageSelector.cs`

```csharp
    public static UpdatePackage? SelectNewest(IEnumerable<FileEntry> files, string product)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(product);
        UpdatePackage? newest = null;
        var prefix = product + "_";
        foreach (var file in files)
        {
            if (file is null || file.Type != "file" || string.IsNullOrEmpty(file.Name) ||
                !file.Name.StartsWith(prefix, StringComparison.Ordinal) ||
                !file.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)) continue;
            var versionText = file.Name[prefix.Length..^4];
            if (!PackageVersion.TryParse(versionText, out var version)) continue;
            if (newest is null || version.CompareTo(newest.Version) > 0)
                newest = new(file, version);
        }
        return newest;
    }
```

The selector considers only files with the exact product prefix followed by `_` and a ZIP extension. Product matching is case-sensitive; the ZIP extension is case-insensitive.

For `Faktury_008.000.043.zip`, `file.Name[prefix.Length..^4]` removes `Faktury_` and the last four characters, `.zip`, leaving `008.000.043`.

Entries with invalid names or versions are skipped; the selector does not currently produce a per-file diagnostic. If versions tie, the first matching entry is retained. The server modification date is not used because a recently copied file can still contain an older version.

This method chooses a package; it does not make HTTP requests, compare with the installed version, or download anything.

## 9. PathRules.cs - validation of names and paths

Source: `src/Tytan.Updater/PathRules.cs`

```csharp
namespace Tytan.Updater;

internal static class PathRules
{
    public static void ValidateSegment(string? value, string label)
    {
        if (string.IsNullOrWhiteSpace(value) || value is "." or ".." ||
            value.Any(c => char.IsControl(c) || "<>:\"/\\|?*".Contains(c)) ||
            value.EndsWith('.') || value.EndsWith(' '))
            throw new ArgumentException($"{label}: invalid name.");
        var stem = value.Split('.')[0];
        if (new[] { "CON", "PRN", "AUX", "NUL" }.Contains(stem, StringComparer.OrdinalIgnoreCase) ||
            (stem.Length == 4 && (stem.StartsWith("COM", StringComparison.OrdinalIgnoreCase) ||
             stem.StartsWith("LPT", StringComparison.OrdinalIgnoreCase)) && stem[3] is >= '1' and <= '9'))
            throw new ArgumentException($"{label}: reserved name.");
    }

    public static void ValidatePackage(FileEntry entry, string folder)
    {
        ValidateSegment(folder, "Client folder");
        ValidateSegment(entry.Name, "File");
        if (entry.Path != folder + "/" + entry.Name || entry.Type != "file" || entry.Size is < 0)
            throw new InvalidDataException("The package does not belong to the requested folder or contains invalid data.");
    }
}
```

`ValidateSegment` checks one name, not a nested path. It rejects empty names, `.` and `..`, separators, Windows-invalid characters, trailing periods or spaces, and reserved device names such as `NUL`.

`ValidatePackage` requires the remote path to equal the requested folder plus `/` plus the filename. For client `Barcin_Wodbar`, a path for another client or one containing traversal is rejected before download.

These checks implement the documented single-level client-folder layout. They do not configure permissions on the server or replace server-side access controls.

## 10. PackageDownload.cs - temporary file and validation

This file declares `partial class UpdateApiClient`. It is another part of the same class, split into a separate file for readability; it can use the private HttpClient and request helpers declared in UpdateApiClient.cs.

### Preparing the destination

Source: `src/Tytan.Updater/PackageDownload.cs`

```csharp
        var destination = Path.GetFullPath(destinationDirectory);
        cancellationToken.ThrowIfCancellationRequested();
        Directory.CreateDirectory(destination);
        var finalPath = Path.Combine(destination, package.File.Name);
        if (File.Exists(finalPath) || Directory.Exists(finalPath))
            throw new IOException("The destination file already exists; it will not be overwritten.");
        var temporary = Path.Combine(destination, $".tytan-{Guid.NewGuid():N}.part");
```

The destination is converted to an absolute path and created if necessary. The final path uses the validated remote filename. Existing files or directories are not overwritten or reused.

Each operation gets a unique `.tytan-<id>.part` file inside the destination folder. The temporary and final files are on the same filesystem, allowing publication by a same-directory move.

### Streaming the download and checking its size

Source: `src/Tytan.Updater/PackageDownload.cs`

```csharp
            using var request = CreateRequest("download.php?file=" + Uri.EscapeDataString(package.File.Path));
            using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token);
            CheckResponse(response);
            await using (var source = await response.Content.ReadAsStreamAsync(token))
            await using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                81920, FileOptions.Asynchronous))
            {
                await source.CopyToAsync(output, token);
                await output.FlushAsync(token);
                if ((package.File.Size is long expected && output.Length != expected) ||
                    (response.Content.Headers.ContentLength is long length && output.Length != length))
                    throw new InvalidDataException("The downloaded size does not match the expected size.");
            }
```

The selected relative path is encoded in `download.php?file=...`. The response stream is copied to a newly created temporary file. Streaming avoids holding the full package in memory; FileShare.None prevents another process from using that open temporary file.

The byte count is compared with the listing's size and the response Content-Length when available. If either differs, the package is rejected. If neither is available, ZIP readability is still checked.

The linked timeout covers the request, body copying, and ZIP validation. Cancellation is passed to the asynchronous operations.

### Reading the ZIP, publishing, and cleanup

Source: `src/Tytan.Updater/PackageDownload.cs`

```csharp
            await ValidateZipAsync(temporary, token);
            token.ThrowIfCancellationRequested();
            // Same-directory move publishes only complete files, and never replaces another download.
            File.Move(temporary, finalPath, overwrite: false);
            return finalPath;
        }
        finally
        {
            // Delete only this operation's temporary file; preserve the original failure if cleanup fails.
            try { if (File.Exists(temporary)) File.Delete(temporary); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }
```

Source: `src/Tytan.Updater/PackageDownload.cs`

```csharp
    private static async Task ValidateZipAsync(string path, CancellationToken token)
    {
        using var archive = ZipFile.OpenRead(path);
        if (archive.Entries.Count == 0) throw new InvalidDataException("The ZIP archive contains no files.");
        foreach (var entry in archive.Entries)
        {
            token.ThrowIfCancellationRequested();
            await using var stream = entry.Open();
            await stream.CopyToAsync(Stream.Null, token);
        }
    }
```

ZIP validation opens the archive, rejects an archive with zero entries, and reads each entry into Stream.Null. This checks that entries can be read and decompressed without extracting files to disk. It does not validate Tytan-specific package contents, execute code, or verify a signature or cryptographic hash.

After validation, the code checks cancellation and moves the temporary file to the final path without replacement. If two operations target the same final filename, only one can publish it; the other returns an error rather than overwriting it.

`finally` attempts to delete only this operation's temporary file. Cleanup failures caused by permissions or I/O are suppressed to preserve the original result, so a partial file can remain if the operating system prevents deletion. It is never returned as a ready package.

## 11. CLI Program.cs - commands and console output

The command-line program is a way to exercise the library without Tytan. It shows help for no arguments or `--help`, rejects unsupported argument layouts, and supports:

| Command | Behavior |
| --- | --- |
| `list <client-folder>` | Query and print the remote listing; no download |
| `download <client-folder> <product> <installed-version> <destination>` | Run the service and print its result |
| `demo <destination>` | Use an in-memory handler, without real credentials or network access |

### Reading configuration

Source: `src/Tytan.Updater.Cli/Program.cs`

```csharp
    var demo = args[0] == "demo";
    var username = demo ? "demo" : Environment.GetEnvironmentVariable("TYTAN_USERNAME");
    var password = demo ? "demo" : Environment.GetEnvironmentVariable("TYTAN_PASSWORD");
    if (string.IsNullOrWhiteSpace(username) || password is null)
    {
        Console.Error.WriteLine("Set TYTAN_USERNAME and TYTAN_PASSWORD outside the repository.");
        return 2;
    }
    var baseUrl = demo ? "https://demo.invalid/SQLupdate/" :
        Environment.GetEnvironmentVariable("TYTAN_BASE_URL") ?? "https://tytan.poznan.pl/SQLupdate/";
    using var api = new UpdateApiClient(new Uri(baseUrl), username, password, demo ? new DemoHandler() : null);
```

Real operations read `TYTAN_USERNAME`, `TYTAN_PASSWORD`, and optionally `TYTAN_BASE_URL`. The default URL comes from the documents. Demo mode substitutes an in-memory handler; `demo.invalid` is never contacted.

### Calling the service and returning an exit code

Source: `src/Tytan.Updater.Cli/Program.cs`

```csharp
    var request = demo ? new UpdateRequest("demo", "Demo", "001.000.001", args[1]) :
        new UpdateRequest(args[1], args[2], args[3], args[4]);
    var result = await new UpdateService(api).CheckAndDownloadAsync(request, cancellation.Token);
    Console.WriteLine(JsonSerializer.Serialize(result, json));
    return result.Status switch { UpdateStatus.Error => 1, UpdateStatus.Cancelled => 130, _ => 0 };
```

The result is printed as indented JSON. JsonStringEnumConverter outputs names such as `Downloaded` rather than enum numbers. The CLI returns 0 for success or no update, 1 for service errors, 2 for invalid CLI arguments/configuration, and 130 for caller-requested cancellation.

### Ctrl+C cancellation

Source: `src/Tytan.Updater.Cli/Program.cs`

```csharp
using var cancellation = new CancellationTokenSource();
ConsoleCancelEventHandler cancel = (_, e) => { e.Cancel = true; cancellation.Cancel(); };
Console.CancelKeyPress += cancel;
```

The event handler prevents immediate process termination and cancels the token passed through the service to HTTP and file operations. The handler is removed in `finally` after the CLI operation ends.

## 12. DemoHandler.cs - a simulated server

Source: `src/Tytan.Updater.Cli/DemoHandler.cs`

```csharp
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        HttpContent content = request.RequestUri!.AbsolutePath.EndsWith("api.php", StringComparison.Ordinal)
            ? new StringContent(JsonSerializer.Serialize(new[] {
                new FileEntry("Demo_001.000.002.zip", "file", package.Length, null, "demo/Demo_001.000.002.zip") }))
            : new ByteArrayContent(package);
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = content });
    }
```

The constructor creates a ZIP in memory containing DEMO.txt. When the client requests api.php, this handler returns a JSON listing for Demo_001.000.002.zip. Other requests in the demo return its bytes.

This exercises the normal service and download code, replacing only the HTTP transport. It does not prove that the production API works and must not be used as a real server implementation.

## 13. Local tests

Tests run as a console executable, not through dotnet test. A failed case produces a nonzero exit code. StubHandler returns controlled HTTP responses; InterruptedStream throws an IOException to simulate a broken transfer. Test files use temporary directories and are cleaned up afterward.

### Example: the full download flow

Source: `tests/Tytan.Updater.Tests/Program.cs`

```csharp
AsyncTest("Full flow publishes a valid ZIP and leaves installed version unchanged", () => WithDirectory(async directory =>
{
    var bytes = MakeZip();
    using var client = DownloadClient(bytes);
    var result = await new UpdateService(client).CheckAndDownloadAsync(new("cliente", "Faktury", "008.000.042", directory));
    Equal(UpdateStatus.Downloaded, result.Status);
    Equal("008.000.042", result.InstalledVersion);
    Equal("008.000.043", result.AvailableVersion);
    Equal(true, bytes.SequenceEqual(await File.ReadAllBytesAsync(result.LocalPath!)));
    Equal(1, Directory.GetFiles(directory).Length);
}));
```

This test creates a real sample ZIP, supplies a simulated listing and download, invokes the service, and checks the result, versions, byte-for-byte file content, and absence of extra temporary files.

The installed version must remain unchanged: downloading does not mean installing.

The recorded 15 passing cases cover numeric comparison, invalid versions, product selection, authentication and JSON mapping, HTTP errors, invalid folders and URLs, complete downloads, no-update behavior, invalid ZIPs and sizes, interruptions, cross-client paths, existing files, cancellation, concurrent publication, and invalid input.

Permission errors, every operating-system failure, live server compatibility, and installation inside Tytan are not all demonstrated by those 15 cases.

## 14. Calling the library from Tytan

The following is an integration example, not a standalone program. Tytan must supply the named configuration variables and its cancellation token. Reuse the API client for the component's lifetime.

```csharp
using Tytan.Updater;

// Reuse the client for the component's lifetime and dispose it when finished.
using var api = new UpdateApiClient(
    new Uri("https://tytan.poznan.pl/SQLupdate/"),
    usernameFromConfiguration,
    passwordFromConfiguration);

var service = new UpdateService(api);
var result = await service.CheckAndDownloadAsync(
    new UpdateRequest(clientFolder, product, installedVersion, localUpdatesDirectory),
    cancellationToken);

switch (result.Status)
{
    case UpdateStatus.Downloaded:
        // Pass result.LocalPath and result.AvailableVersion to Tytan's existing workflow.
        // Only Tytan applies the ZIP and updates its installed-version record.
        break;
    case UpdateStatus.NoUpdate:
        // Continue without installing. Message distinguishes no package from no newer version.
        break;
    case UpdateStatus.Cancelled:
    case UpdateStatus.Error:
        // Display or log result.Message.
        break;
}
```

Tytan should hand LocalPath to its existing installer only for Downloaded. It should not change its installed-version record merely because AvailableVersion is populated or a ZIP was downloaded.

## 15. Commands to try

From the repository root:

```powershell
dotnet build Tytan.Updater.sln --configuration Release
dotnet run --project tests/Tytan.Updater.Tests --configuration Release
dotnet run --project src/Tytan.Updater.Cli -- --help
dotnet run --project src/Tytan.Updater.Cli -- demo ./downloads/demo
```

The demo creates Demo_001.000.002.zip. Repeating it with the same destination reports an existing-file error and preserves the file. Server commands and credential setup are documented in docs/uso.md.

## 16. Current limits and next steps

- Confirm the exact API and download paths because the documented listing URL returned HTTP 404.
- Validate the actual JSON and package naming against the production server before claiming live compatibility.
- Check the library's .NET compatibility with Tytan's project and connect the request/result contract to its existing update workflow.
- Tytan supplies the installed version and applies the ZIP; there is no graphical UI in this repository.
- There are no automatic retries, download resume, package hash/signature verification, or automatic replacement of existing ZIPs.
- Invalid filenames are skipped during selection without a per-file diagnostic.
- Reading a ZIP confirms readability, not product compatibility or successful installation.

Suggested reading order: Models.cs, UpdateService.cs, UpdateApiClient.cs, PackageSelector.cs, PackageVersion.cs, PackageDownload.cs, then the CLI and tests.
