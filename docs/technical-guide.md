# Tytan Updater — Technical guide

Updated: October 9, 2026. Language: English.

This guide describes the retained first Windows distribution, TytanUpdater-win-x64-20261009-094714-e05a10, and the source restored by commit 9001bc9. Its application behavior matches the initial distribution commit 45122ab. Documentation changes do not change the executables or the retained ZIP.

## Scope and implemented behavior

The system publishes existing Tytan update ZIPs over HTTPS, compares available package versions against explicitly supplied installed versions, and downloads newer packages. It consists of a PHP server, a Windows Forms client, and a Windows console client. The console also uses a Windows Save As dialog.

The desktop reads a provisional local installation JSON; the console derives one product/version from its second argument. Neither independently detects installed software, extracts packages, runs installers, updates the recorded installed version, or modifies the server package store. The definitive installed-version file and Tytan installation mechanism remain external integration work.

All customers use the shared hosting account as requested. Local client-folder selection determines the API directory queried; it does not enforce per-customer server authorization. There is no database or user-to-folder mapping in this implementation.

## Architecture and responsibilities

```text
Desktop: installation JSON --> LocalInstallation ----+
                                                    |
CLI: customer + product_version --> LocalInstallation+
                                                    v
                         CloudApiClient.ListAsync -- HTTPS --> api.php
                                                    |             |
                                              RemoteEntry[]   common.php
                                                    |             |
                         UpdateComparison.Compare <-+         ZIP folders
                                                    |
                                         newer ProductUpdate
                                                    |
                                        Windows Save As dialog
                                                    |
                     CloudApiClient.DownloadAsync -- HTTPS --> download.php
                                                    |
                             unique .part -> checks -> final ZIP
```

The PHP server knows directories and files, not the installed client version. C# performs version selection. The Save As choice determines the local output path and is never sent as a server filesystem path.

| Source | Responsibility |
| --- | --- |
| server/api.php | Enumerate a requested directory and return JSON metadata |
| server/download.php | Stream a requested existing ZIP |
| server/common.php | Authentication, HTTPS, methods, paths, and errors |
| desktop/Tytan.Updater.Desktop/Program.cs | Start WinForms or verification modes |
| desktop/Tytan.Updater.Desktop/MainForm.cs | UI state, file selection, querying, comparison display, downloading |
| desktop/Tytan.Updater.Desktop/InstallationFileReader.cs | Validate provisional JSON and produce local models |
| desktop/Tytan.Updater.Desktop/UpdateComparison.cs | Numeric version parsing and package selection |
| desktop/Tytan.Updater.Desktop/CloudApiClient.cs | HTTPS listing and metadata validation |
| desktop/Tytan.Updater.Desktop/PackageDownload.cs | Streaming, progress, ZIP validation, and publication |
| cli/Tytan.Updater.Cli/Program.cs | Console entry, credentials from environment, Ctrl+C |
| cli/Tytan.Updater.Cli/Command.cs | Two-argument input, query/compare/download orchestration, exit codes |
| cli/Tytan.Updater.Cli/DownloadLocation.cs | Windows Save As on a dedicated STA thread |
| scripts/publish_windows.ps1 | Publish two portable executables and package them |

The CLI project links the existing reader, comparison, listing, and download source files through its csproj. These files keep their Tytan.Updater.Desktop namespace; linking does not open the main desktop window or create a second implementation.

## Runtime and delivery

Both projects target net9.0-windows with Windows Forms enabled. The desktop OutputType is WinExe; the CLI OutputType is Exe, so it retains terminal output. Development requires the .NET 9 SDK on Windows. The published win-x64 delivery includes its runtime and does not require the SDK or a separate .NET installation on the customer's PC.

The PHP endpoint implementation is compatible with PHP 7.2.34, the hosting version reported during integration. It uses JSON and filesystem functions, without Composer, a database, or PHP's ZIP extension. PHP streams packages without opening their archive contents.

```text
Customer delivery:
    Tytan.Updater.Desktop.exe
    Tytan.Updater.Cli.exe
    installation.example.json
    README.txt

Hosting SQLupdate directory:
    api.php
    download.php
    common.php
    .htaccess                 existing hosting configuration
    Barcin_Wodbar/
        Faktury_008.000.043.zip
        FK2025_005.005.007.zip
        FK2026_005.005.040.zip
```

Only the three PHP files belong on hosting. Customer executables, local JSON files, tests, documentation, and the source repository do not belong in SQLupdate.

## Local installation data and desktop input

InstallationFileReader.cs defines the models consumed by comparison and download code:

```csharp
internal sealed record InstalledProduct(string Name, string InstalledVersion);
internal sealed record LocalInstallation(string ClientFolder, IReadOnlyList<InstalledProduct> Products);
```

Read(path) reads text and delegates to Parse(json). The JSON-to-model mapping is isolated from the window so that a future definitive file format can replace this reader. The current input is explicit information, not discovered installation state.

```json
{
  "clientFolder": "Barcin_Wodbar",
  "products": [
    { "name": "Faktury", "installedVersion": "008.000.042" },
    { "name": "FK2025", "installedVersion": "005.005.007" },
    { "name": "FK2026", "installedVersion": "005.005.039" }
  ]
}
```

The sample above is demonstration data. Supply the real client's folder and installed versions when checking an actual installation. Multiple products are supported by the desktop JSON. The exact lowercase property names are mapped using JsonPropertyName attributes.

Parse uses a JSON depth limit of 16 and rejects unmapped fields. It rejects a null document, an invalid client folder, an empty/null product collection, null product entries, invalid product names, case-insensitive duplicate product names, and versions outside NNN.NNN.NNN. JsonException becomes InvalidDataException with an English format error.

```csharp
var options = new JsonSerializerOptions
{
    MaxDepth = 16,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
};
```

ValidName permits a single name segment, excludes a leading dot, trailing dots/spaces, controls, slash/backslash, colon, and Windows-invalid filename characters. Thus neither a client name nor a product name can become an absolute path or traversal request.

```csharp
return !string.IsNullOrWhiteSpace(name) && name[0] != '.' &&
    name == name.TrimEnd(' ', '.') &&
    !Regex.IsMatch(name, "[\\x00-\\x1F\\x7F<>:\"\\\\|?*/]");
```

The desktop does not save credentials or remember an installation configuration in this retained version. Each launch requires loading a file, choosing the example, or supplying an installation file argument at startup.

## Desktop startup and UI orchestration

Program.Main has STAThread, initializes Windows Forms, and normally runs MainForm. Zero normal arguments open an empty window; one argument supplies an initial installation file path. Unsupported normal arguments show usage. Verification switches are described below.

MainForm owns one CloudApiClient, the current LocalInstallation, the latest comparison results, and an operation CancellationTokenSource. Its two tabs separate installed-product comparison from the complete cloud metadata listing. The cloud tab is a listing of the loaded client's entries, not a root-folder browser.

| MainForm method or property | Behavior |
| --- | --- |
| Constructor | Build controls, wire events, optionally load a startup file on Shown |
| ChooseFile | Open a JSON file chooser and pass the selected file to TryLoad |
| TryLoad | Catch file/format/access errors and preserve previously valid state |
| LoadInstallation | Validate the complete file before replacing installation/grid state; clear prior cloud/download state; label example data |
| QueryCloudAsync | Set busy state, clear stale results, list the loaded folder, compare versions, render rows, and report errors/cancellation |
| ResetComparisons | Clear candidates and restore Not checked cells before a refresh |
| DisplayComparisons | Map results by product name, preserving correct results after grid sorting; highlight newer versions |
| SetBusy | Disable file/credential/product controls during work, enable Cancel, and recalculate buttons |
| SelectedUpdate | Return the selected installed-product candidate only if it is newer and the installed-products tab is active |
| UpdateDownloadButton | Enable downloading only for an eligible selected result while idle |
| ChooseDownloadAsync | Open Save As with the selected package name and invoke DownloadSelectedAsync |
| DownloadSelectedAsync | Start progress/cancellation, call the shared downloader, show the final path and completion state, handle failures |
| OpenDownloadFolder | Open the saved ZIP's containing directory through the shell on an explicit user click |
| OnFormClosing | Cancel an active operation unless closing was itself cancelled |
| Dispose | Cancel pending work and dispose the HTTP client |
| SetCredentials / CancelCloudQuery | Internal helpers used by verification |
| Inspection properties | Expose grid counts, statuses, selected versions, saved path, and progress to checks; they are not API endpoints |

The cloud call and comparison in QueryCloudAsync connect the transport layer to the UI:

```csharp
IReadOnlyList<RemoteEntry> entries = await api.ListAsync(
    installation.ClientFolder, usernameBox.Text, passwordBox.Text, cancellation.Token);
cancellation.Token.ThrowIfCancellationRequested();
if (!IsDisposed)
{
    comparisons = UpdateComparison.Compare(installation, entries);
    DisplayComparisons();
```

This is an excerpt; the remaining method displays metadata and a count of available updates, handles cancellation/timeouts/HTTP/format errors, and releases busy state in finally. Checking does not download automatically. The user selects an eligible row and presses Download selected update.

If loading a replacement file fails, the previous valid installation remains displayed. If an online refresh fails, old available-version results are cleared so an earlier successful query cannot remain presented as a fresh result.

## Console input and execution

The CLI uses exactly two positional arguments:

```powershell
.\Tytan.Updater.Cli.exe Barcin_Wodbar Faktury_008.000.042
```

The first is one cloud client folder name. The second is a name, not a path: the last underscore separates the product from the installed version. No JSON or actual folder with this name is needed. No third destination argument is supported.

Command.RunAsync validates the inputs and creates the same local model that the desktop reader produces:

```csharp
var product = new InstalledProduct(installedFolder[..separator], installedFolder[(separator + 1)..]);
var installation = new LocalInstallation(args[0], new[] { product });
```

Using the last underscore supports names such as Product_With_Underscores_008.000.042. A full filesystem path, a ZIP extension, a missing product prefix, or a malformed version is rejected before HTTP.

Program reads TYTAN_API_USERNAME (default TytanSQL) and TYTAN_API_PASSWORD (required). It installs a Console.CancelKeyPress handler that cancels the shared token. RunAsync lists, compares, writes the comparison line, and opens Save As only for a newer package. If the server version is equal or older it writes No updates available. A missing matching package is an error, not an up-to-date result.

```csharp
string? destination = await (chooseDestination ?? DownloadLocation.ChooseAsync)(update.Package.Name, token);
token.ThrowIfCancellationRequested();
if (destination is null)
{
    await output.WriteLineAsync("Download cancelled. No package was downloaded.");
    return 130;
}
```

The chooser is injectable for local tests. The production implementation creates SaveFileDialog on a dedicated background STA thread, because async console continuations run on thread-pool threads. TaskCompletionSource returns the selected full path; WaitAsync(token) allows Ctrl+C to end the wait. The background thread does not keep the CLI process alive when Main finishes. An interactive Windows session is required.

TerminalProgress reports stage changes and roughly ten-percentage-point increments synchronously, avoiding delayed progress callbacks after console completion.

| Exit code | Meaning |
| --- | --- |
| 0 | Valid comparison found no newer version, or a newer package downloaded successfully |
| 1 | Input/data/credentials/network/timeout/ZIP/destination error, including missing matching packages |
| 2 | Wrong argument count or unsupported normal invocation |
| 130 | Ctrl+C cancellation or cancellation of Save As |

--help/-h prints help without credentials or a request. --self-test runs local checks. Double-clicking the console EXE without arguments prints usage then exits; run it from PowerShell to see the output and provide parameters.

## HTTPS listing and JSON contract

CloudApiClient's default base URL is https://tytan.poznan.pl/SQLupdate/. Its constructor requires an absolute HTTPS URL ending in a slash, without embedded credentials, query, or fragment. When it creates HttpClient itself, redirects are disabled and header/request timeout is 30 seconds. An injected HttpClient remains the caller's resource; Dispose only disposes an owned client.

ListAsync additionally covers the full listing body with a linked 30-second deadline. It sends a per-request UTF-8 Basic Authorization header and Accept: application/json:

```csharp
var endpoint = new Uri(baseUri, "api.php?dir=" + Uri.EscapeDataString(clientFolder));
using var request = new HttpRequestMessage(HttpMethod.Get, endpoint);
request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
request.Headers.Authorization = new AuthenticationHeaderValue("Basic",
    Convert.ToBase64String(Encoding.UTF8.GetBytes(username + ":" + password)));
```

Only the specified client folder is requested. Root listing is supported by PHP for other consumers but is not used by these Windows workflows. Uri.EscapeDataString encodes the supplied name; it is not concatenated as an unescaped query value.

The server returns an array, including an empty array for an empty directory:

```json
[
  {
    "name": "Faktury_008.000.043.zip",
    "type": "file",
    "size": 17492922,
    "modified": "2026-10-02 06:57:29",
    "path": "Barcin_Wodbar/Faktury_008.000.043.zip"
  }
]
```

The example metadata comes from the recorded integration listing and is not a live availability guarantee. Size is bytes. Folder entries have type folder and size null. Modified is a UTC timestamp formatted yyyy-MM-dd HH:mm:ss; path is relative to the server update root.

RemoteEntry uses JsonPropertyName attributes for these lowercase fields. The client requires application/json, reads at most 8 MiB, rejects malformed/null arrays and null entries, and uses depth 16. Every entry must have a valid unique name, an exact clientFolder/name path, a valid timestamp, and either ZIP-file metadata with nonnegative size or folder metadata with null size. Unexpected HTML, cross-client metadata, and duplicate names are rejected.

## Numeric version selection

PackageVersion.TryParse accepts exactly three three-digit numeric components separated by dots. It uses invariant numeric parsing; ToString returns the fixed-width representation. CompareTo evaluates major, then minor, then patch:

```csharp
public int CompareTo(PackageVersion other)
{
    int result = Major.CompareTo(other.Major);
    if (result == 0)
    {
        result = Minor.CompareTo(other.Minor);
    }
    return result != 0 ? result : Patch.CompareTo(other.Patch);
}
```

UpdateComparison.Compare processes each installed product independently. It accepts a file only if the complete product_ prefix and .zip suffix match (case-insensitive), its path is exactly inside the loaded client directory, and the version substring parses. Folder entries, malformed names, unrelated products, and cross-client paths are ignored as candidates. It selects the highest numeric version; modification dates do not choose the update. Equal candidate versions have a deterministic ordinal-path tie-break.

```csharp
string text = entry.Name[prefix.Length..^4];
if (!PackageVersion.TryParse(text, out PackageVersion candidate))
{
    continue;
}
```

| Supplied installed version | Newest matching server version | Result |
| --- | --- | --- |
| 008.000.042 | 008.000.043 | Update available; eligible for downloading |
| 008.000.043 | 008.000.043 | Up to date; no download |
| 008.000.044 | 008.000.043 | Installed version is newer; no downgrade |
| 008.000.042 | No valid matching ZIP | No package; no download |

ProductUpdate retains the original InstalledProduct, selected RemoteEntry, parsed available version, and UpdateStatus. Neither comparison nor download changes the original installed-version data.

## Streaming, validation, and final ZIP publication

PackageDownload.cs extends the same partial CloudApiClient class. DownloadAsync accepts client folder, ProductUpdate, destination file path, credentials, optional progress, and a cancellation token. It returns the saved absolute ZIP path only after validation and publication.

Before HTTP, it re-compares the candidate against the product and client to confirm that it is still a newer eligible package; requires positive listed size and valid credentials/names; canonicalizes the destination; requires a .zip filename in an existing parent directory; and rejects any existing file or directory at that destination.

```csharp
string temporary = target + "." + Guid.NewGuid().ToString("N") + ".part";
```

The temporary file is a unique sibling of the target, opened with CreateNew, write-only access, no sharing, asynchronous/sequential flags, and a 64 KiB buffer. The request targets download.php?file=<encoded-relative-package-path>. It requires HTTP 200 and application/octet-stream. Redirects and partial HTTP 206 responses are rejected; download resume is not implemented.

A linked deadline covers the operation for 15 minutes, while the default HttpClient retains its shorter header/request timeout. Content-Length, when provided, must equal the listing size. Stream reads cannot exceed the listed size, and total received bytes must equal it at EOF. Output is flushed and closed before ZIP validation begins.

```csharp
progress?.Report(new DownloadProgress(received, expected, "Validating ZIP"));
await Task.Run(() => ValidateZip(temporary, token), token).ConfigureAwait(false);
token.ThrowIfCancellationRequested();
// File.Move without overwrite also protects a destination created during transfer.
File.Move(temporary, target, overwrite: false);
created = false;
progress?.Report(new DownloadProgress(received, expected, "Downloaded"));
return target;
```

ValidateZip opens the archive without extraction, requires at least one file entry, reads every entry fully, checks declared versus read lengths, checks cancellation between reads, and limits total expanded content to 2 GiB. Running it in Task.Run keeps this work off the UI thread. These are size/readability checks; the implementation does not provide a package signature, cryptographic authenticity check, or explicit CRC verification guarantee.

Publication uses a non-overwriting rename, so a destination created by another process during transfer is preserved. On a failed/cancelled operation, finally removes only this operation's owned temporary file. The code does not execute a downloaded file.

DownloadProgress reserves 100% for a successfully published package:

```csharp
public int Percent => Stage == "Downloaded" ? 100
    : Total > 0 ? Math.Clamp((int)((double)Bytes / Total * 100), 0, 99) : 0;
```

The desktop reports transfer completion followed by ZIP checking, then displays the saved path and Automatic installation is not available. The full bar remains after completion. Open download folder opens the containing directory, not an installer. Progress callbacks verify the active cancellation-source identity so queued callbacks from an earlier operation cannot overwrite a later UI state.

## PHP listing and download endpoints

api.php and download.php define TYTAN_ENDPOINT and require common.php. Both call bootstrap before resolving the request path.

```php
$base = bootstrap();
$relative = query_path('dir', true);
$target = resolve_target($base, $relative);
```

For api.php, an omitted/empty dir selects the configured root. A nested public relative directory is also supported by PHP, although the Windows clients select a single client segment. The endpoint requires a readable directory, scans it, skips nonpublic/unreadable/out-of-root entries, and publishes directories and ZIP files only. It excludes PHP source, configuration, dotfiles, and non-ZIP files. It resolves symlinks through realpath before checking containment. Each accepted item is mapped to the JSON fields above; [] represents an empty listing.

download.php requires a nonempty file path, a readable regular file, and .zip extensions on both the requested and resolved file. It opens rb, obtains size through fstat on the open handle, supplies binary/download/length/no-cache headers, clears PHP output buffering, and streams with fpassthru. HEAD returns headers without the body. The finally block closes the stream. It serves an existing archive; it does not generate or install one.

```php
header('Content-Length: ' . $stat['size']);
```

Content-Disposition includes a sanitized fallback filename and an encoded UTF-8 filename. If streaming breaks after headers, the client detects incomplete bytes against the listed size; PHP cannot reliably replace an already-started binary response with a JSON error.

| Request | Meaning |
| --- | --- |
| GET /SQLupdate/api.php | Root folder/ZIP metadata listing |
| GET /SQLupdate/api.php?dir=Barcin_Wodbar | Entries in that client folder |
| GET /SQLupdate/download.php?file=Barcin_Wodbar/Faktury_008.000.043.zip | Selected ZIP bytes |
| HEAD on either endpoint | Headers without response body |

## common.php method reference

Direct access to common.php returns 404 unless TYTAN_ENDPOINT was defined by an endpoint. It is shared code, not a third public API operation.

| Function | Detailed responsibility |
| --- | --- |
| send_json(array, status) | JSON-encode data, explicitly check PHP 7.2 encoding failures, set status/content type/no-store/nosniff/length, suppress body for HEAD, and exit |
| fail_request(status, message) | Return an error object through send_json and terminate the request |
| authenticate() | Use trusted REMOTE_USER when neither server credential variable is set; otherwise require both variables and validate request BasicAuth |
| bootstrap() | Hide displayed PHP errors, install private exception logging, restrict methods, require HTTPS, authenticate, resolve/read-check update root, and return its canonical path |
| query_path(parameter, allowEmpty) | Require a scalar string, optionally permit root, split on slash, and validate every segment |
| public_name(name) | Reject empty/dot-prefixed names, traversal, path/control/invalid characters, trailing dots/spaces |
| inside_root(path, base) | Test canonical root equality or a prefix ending at a directory separator |
| resolve_target(base, relative) | Use realpath and inside_root; return the existing contained target or fail with 404 |

Containment uses a separator boundary, so SQLupdate-other does not count as a child of SQLupdate:

```php
return $path === $base || strpos($path, rtrim($base, '/\\') . DIRECTORY_SEPARATOR) === 0;
```

This uses PHP 7.2-compatible strpos rather than PHP 8-only str_starts_with. json_encode errors are checked explicitly because PHP 7.2 lacks JSON_THROW_ON_ERROR.

bootstrap accepts GET and HEAD, returning 405 plus Allow for other methods. HTTPS is determined from the trusted server HTTPS flag or port 443. The only HTTP exception requires PHP's local CLI server, an explicit TYTAN_ALLOW_LOCAL_HTTP=1, and a loopback remote address. Client-supplied forwarding headers do not establish trusted HTTPS state.

Unexpected exceptions are logged privately with class, message, file, and line; clients receive a generic 500 when headers have not already been sent. Diagnostic PHP paths are not returned to callers.

## Authentication and configuration

HTTP BasicAuth carries Base64(username:password). Base64 is an encoding, not password encryption. HTTPS encrypts the transport, with standard certificate validation enabled by the Windows HTTP client. The client creates Authorization per request and does not follow redirects automatically.

The existing hosting configuration supplied during integration uses AuthBasicProvider web-user and Require valid-user. In hosting mode that provider verifies the credentials before PHP, and trusted REMOTE_USER signals authentication to the endpoint. The provider's internal password storage format is outside this repository and is not inferred here.

In PHP-managed mode, both TYTAN_API_USERNAME and TYTAN_API_PASSWORD must be configured on the server. authenticate reads PHP_AUTH_USER/PHP_AUTH_PW or parses an Authorization header, strictly decodes Basic credentials, splits on the first colon, and compares each value with hash_equals. Wrong credentials return 401 and a WWW-Authenticate challenge. Missing or partial server configuration fails with 503. Hosting access controls should also protect directly addressable ZIPs, or packages should be outside the public document root.

| Variable | Side | Purpose |
| --- | --- | --- |
| TYTAN_API_USERNAME | Windows process | Default/prefilled username; defaults to TytanSQL |
| TYTAN_API_PASSWORD | Windows process | Required CLI password; optional desktop field prefill |
| TYTAN_API_USERNAME / TYTAN_API_PASSWORD | PHP server environment | Alternative PHP-managed credential validation |
| TYTAN_UPDATE_ROOT | PHP server | Optional absolute package root; defaults to the PHP script directory |
| TYTAN_ALLOW_LOCAL_HTTP | Local PHP test server | Explicit loopback-only HTTP test exception |
| TYTAN_DESKTOP_CAPTURE | Verification process | Optional application-only PNG capture |

The same variable names on Windows and hosting refer to separate processes and configuration locations. Setting the Windows environment does not configure the hosting server. No real password is included in these guides or distribution files.

## Error handling and operational examples

| Condition | Response or client behavior |
| --- | --- |
| Invalid path syntax / array query parameter | PHP 400 |
| Wrong credentials in PHP-managed mode | PHP 401; hosting mode may reject before PHP |
| HTTP without permitted local-test exception | PHP 403 |
| Missing/out-of-root/unreadable target | PHP 404 |
| Unsupported HTTP method | PHP 405 |
| Unexpected server exception | Generic PHP 500 and private log details |
| Missing/incomplete authentication or unavailable root | PHP 503 |
| Listing HTML, malformed JSON, invalid metadata | English C# validation error |
| Existing local ZIP destination | Preserve existing file and report an error |
| Size mismatch, unreadable ZIP, interrupted transfer | Remove owned partial file; do not publish final ZIP |

For an HTTP 500, inspect the hosting's private PHP error log and confirm the runtime and three deployed files. The earlier PHP 7.2 integration error was resolved by replacing incompatible PHP features. Browser output alone does not prove .htaccess caused an error.

A browser can open the listing URLs and prompt for the hosting credentials. The directory URL must be api.php?dir=<name>, not a local path. download.php?file=<relative ZIP path> starts a browser download. Browser tests validate the endpoints but do not validate the updater's installed-version source or installation behavior.

## Worked comparison and download example

Using the demonstration JSON and the production listing recorded during integration:

| Product | Supplied version | Recorded server ZIP version | Result |
| --- | --- | --- | --- |
| Faktury | 008.000.042 | 008.000.043 | Update available |
| FK2025 | 005.005.007 | 005.005.007 | Up to date |
| FK2026 | 005.005.039 | 005.005.040 | Update available |

The desktop lists all three products. Selecting Faktury and saving as C:\Users\Ibai\Downloads\Faktury_008.000.043.zip causes an authenticated request for Barcin_Wodbar/Faktury_008.000.043.zip, an owned sibling .part write, byte checks, archive reading, and final rename. The JSON remains at 008.000.042 because downloading does not establish installation success.

The equivalent console example checks only Faktury. It gets credentials from the current PowerShell environment, displays its comparison, opens Save As, and reports ZIP saved to followed by the actual selected full path. Running the same supplied version again still finds the update; choosing the same existing output path is an error, not a successful installation.

## Development, packaging, and verification

From the repository root:

```powershell
dotnet build desktop/Tytan.Updater.Desktop --configuration Release
dotnet build cli/Tytan.Updater.Cli --configuration Release
dotnet run --project desktop/Tytan.Updater.Desktop --configuration Release -- --self-test
dotnet run --project cli/Tytan.Updater.Cli --configuration Release -- --self-test
python tests/server/test_endpoints.py --php C:/path/to/php.exe
```

| Check suite | Count in retained Windows version | Coverage |
| --- | --- | --- |
| DesktopChecks | 10 | Reader validation, rendered local data, preserving valid state after invalid replacement |
| ComparisonChecks | 12 | Numeric precedence, product/path isolation, malformed names, equal/newer/missing results |
| CloudChecks | 22 | HTTP/auth/query metadata, redirects, errors, cancellation, timeout, sorting, empty folders |
| DownloadChecks | 23 | ZIP bytes/progress, size/HTTP/ZIP failures, cancellation, existing-file races, UI completion |
| CommandChecks | 24 | Two-name input, authenticated query/download, selected path, chooser cancellation, errors, help |
| Total Windows checks | 91 | Local simulated responses/fixtures; no production requests |

The PHP harness independently launches actual temporary PHP servers, creates owned ZIP fixtures, exercises endpoints/configuration failures, and cleans up. See [verification history](verificacion.md) for historical PHP runtime results. A server-only test run needs Python and PHP, not .NET.

Desktop --verify-live performs a real read-only production listing/comparison; --verify-live-download performs a real Faktury download into an owned temporary verification directory and removes it afterward. They require private process credentials and should be treated as production-facing verification, unlike --self-test. No such request is necessary to regenerate documentation.

The retained release was checked after extraction: its console help, 24 CLI checks, and 67 desktop checks passed. Previous production verification listed the three packages above and downloaded Faktury_008.000.043.zip (17492922 bytes) successfully; installed example information remained unchanged. These are recorded results, not a new live request made for this guide. Acceptance on a representative customer PC remains necessary.

Publishing uses the script below; it creates a new timestamped package when explicitly run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File ./scripts/publish_windows.ps1
```

The essential publish flags are:

```powershell
-p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
-p:PublishTrimmed=false -p:DebugType=None -p:DebugSymbols=false
```

The script uses self-contained Release publishing, defaults to win-x64, optionally accepts win-arm64, checks publish exit codes, copies two EXEs plus example/README, compresses a ZIP, and prints SHA256. It creates unique directories instead of deleting prior releases. Intermediate output is under ignored downloads/release-builds; releases are under ignored dist. Native runtime components may extract on launch. Only win-x64 was verified for the retained package.

The retained original ZIP is approximately 85.3 MiB, with SHA256 29A0E00943D989D0300CA11FE0AC78F8FB306BEB03E90B88733000A2A5F99D7E. It is preserved unchanged; the new user/technical guides are separate documentation artifacts.

## Integration boundaries and future changes

To adapt a definitive installed-version file, replace the JSON mapping in InstallationFileReader while preserving LocalInstallation semantics. To change the production endpoint base, update/inject CloudApiClient's base URI and validate the HTTPS contract. Do not change server version selection: it belongs in the client comparison layer.

Automatic installation requires the real Tytan entry point, required arguments/paths, runtime behavior, success/failure signal, and ownership of installed-version updates. DownloadAsync's verified path can feed that confirmed mechanism. A completed download, a launched executable, or a closed installer window alone must not be treated as installation success. See [installation contract](installation-contract.md).

For end-user instructions, see [User guide](user-guide.md). For the PHP-specific walkthrough with additional endpoint excerpts, see [PHP technical walkthrough](technical-walkthrough.md). Deployment details remain in [Server guide](server.md).
