# Usage and integration with Tytan

## Build and run tests

From the repository root, with the .NET 9 SDK installed:

```powershell
dotnet build Tytan.Updater.sln --configuration Release
dotnet run --project tests/Tytan.Updater.Tests --configuration Release
```

Tests are an executable without external dependencies. They print each case and return exit code 0 if all pass, or 1 if any fail. They are not run using `dotnet test`.

## Local demo

```powershell
dotnet run --project src/Tytan.Updater.Cli -- demo ./downloads/demo
```

The demo simulates listing and downloading in memory without contacting the server. It generates `Demo_001.000.002.zip` containing sample text and returns `Downloaded`. This is not a real Tytan update. Repeating the command with the same destination reports that the file already exists and preserves it.

## Server access

Provide credentials for the PowerShell session without writing them into code or command history:

```powershell
$env:TYTAN_USERNAME = Read-Host 'Username'
$env:TYTAN_PASSWORD = [System.Net.NetworkCredential]::new('', (Read-Host 'Password' -AsSecureString)).Password
$env:TYTAN_BASE_URL = 'https://tytan.poznan.pl/SQLupdate/'

dotnet run --project src/Tytan.Updater.Cli -- list Barcin_Wodbar
dotnet run --project src/Tytan.Updater.Cli -- download Barcin_Wodbar Faktury 008.000.042 ./downloads/Barcin_Wodbar

Remove-Item Env:TYTAN_USERNAME, Env:TYTAN_PASSWORD
```

Environment variables are an option for this CLI; Tytan can supply credentials through its own configuration. `TYTAN_BASE_URL` allows a different published path if the actual address differs from the documents.

The live query on October 5 returned HTTP 404 at the documented address. See [Verification record](verificacion.md). Check that path before expecting a live download; local tests do not validate server publication.

`list` does not download files. `download` checks and downloads only if a newer version of the product is available. Press Ctrl+C to cancel. Exit codes:

| Code | Meaning |
| --- | --- |
| 0 | Success or no update available |
| 1 | Operation failed |
| 2 | Invalid arguments or configuration |
| 130 | User-requested cancellation |

The download command's JSON includes its status. A failed download can return exit code 1 even when caused by invalid input, because the service reports it as `UpdateResult.Error`.

## Calling the module from TytanSQL

Reference `src/Tytan.Updater/Tytan.Updater.csproj` from Tytan's compatible project, or distribute the compiled library. This example illustrates the contract; replace the variables with values already known to the application:

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

`UpdateApiClient.ListAsync()` lists the root; `ListAsync(clientFolder)` queries one folder. `UpdateService` coordinates selection and downloading. Supply the installed version as a string with three numeric components.

## File policy and limits

- Product and folder names must match the server's names; product prefix matching is case-sensitive.
- Client folders have a single level, as in the documents.
- Existing files are neither reused nor overwritten automatically. Tytan decides whether to move or delete them before another download.
- Downloads use their own temporary file and are published by moving it within the same folder without replacing an existing file.
- The module checks size when available and confirms that the ZIP can be read and decompressed into a discarded stream. It does not extract files to disk or verify a signature or hash; the documented API provides neither.
- The default timeout is two minutes per listing or download operation, including reading and validation. It can be configured in the client constructor.
- The client does not follow redirects. An injected test handler must preserve this behavior. There are no automatic retries.
- If the operating system prevents cleanup after a failure, a `.tytan-*.part` file can remain; it is never returned as a ready ZIP.
- Compatibility with Tytan's actual application and installation must be validated in its project. The library currently targets `net9.0`.

## Repository structure

```text
src/Tytan.Updater/         C# library
src/Tytan.Updater.Cli/     CLI tool and demo
tests/Tytan.Updater.Tests/ Local tests without a live server
docs/                     Requirements, phases, usage, and verification
```
