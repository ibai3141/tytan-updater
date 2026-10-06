# Local updater application

## Agreed workflow

The local Windows application will identify the client from local information, query the corresponding folder on the existing cloud API, compare available package versions with locally installed versions, and allow downloading newer ZIPs. Installation of those ZIPs remains a separate responsibility to confirm with Tytan.

All clients will use the same existing BasicAuth account, as explicitly requested. Selecting dir from a local file determines which folder the application displays; it is not server-side customer authorization. Someone with that account can request another folder. The server remains unchanged for this workflow.

The definitive local file is being produced by another developer. Its path, structure, and update rules have not yet been supplied. The client identifier might instead come from a local folder name; this source is also pending. This phase uses an explicit JSON example and does not infer a client from arbitrary folders on the computer.

## Phase 1: local window and installation file

Implemented:

- Windows Forms application targeting the available .NET 9 Windows runtime.
- Open a selected local JSON file or use the clearly labeled example.
- Show the client folder, product names, and installed versions.
- Validate all input before changing the displayed installation.
- Show English errors and preserve the last valid data on a failed replacement.
- Keep JSON mapping in InstallationFileReader so the final format can be substituted.

Phase 1 originally made no API calls. Phase 2 now adds the cloud listing described below. Comparison, downloads, and installation are not yet implemented. Available-version and status cells show Not checked. Loading the example does not detect actual installations on this computer and does not modify the local file.

## Phase 2: HTTPS cloud-folder listing

Load an installation file or the example first. Enter the shared username and password, then click Load cloud folder. The request is:

```text
https://tytan.poznan.pl/SQLupdate/api.php?dir=<loaded-clientFolder>
```

The application never fetches the root client list. Folder values are URL-encoded. The Cloud folder tab displays name, type, size in bytes, modification timestamp in UTC, and relative path. An empty folder is reported explicitly. Installed products and versions remain unchanged.

The password is masked and retained only in process memory; it is not embedded in source or saved to disk. The username defaults to TytanSQL; enter the existing password supplied by the administrator. Optional private process variables TYTAN_API_USERNAME and TYTAN_API_PASSWORD can prefill the fields. Do not commit credential files.

Local-file and credential controls are disabled during a query to prevent changing the client mid-request. Cancel stops the query; closing the window also cancels it. Each new query clears stale cloud results. Errors appear in the status label, leaving valid installed data unchanged; controls are restored afterward.

The 30-second deadline covers both headers and body reading. Automatic redirects are disabled, HTTPS is required, and standard certificate validation remains enabled. HTTP failures, malformed/non-JSON responses, invalid metadata, duplicate entries, and paths outside the loaded folder are rejected. Listings are limited to 8 MiB. Numeric version comparison is not performed yet.

Client-side folder selection remains the expressly requested shared-account design; it does not restrict other callers who have the same credentials.

## Provisional input contract

Source: examples/installation.example.json. These are sample values, not confirmed installed versions.

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

Names must be valid single path/name segments. Installed versions must contain three groups of three digits. Products must be nonempty and unique, ignoring letter case. Unexpected JSON properties are rejected so a mismatched definitive format is reported rather than silently ignored. The format is intentionally provisional; agree it with the other developer before connecting their file.

## Run the window

Requires Windows and the .NET 9 SDK:

```powershell
cd C:\Users\Ibai\tytan-updater
dotnet run --project desktop/Tytan.Updater.Desktop --configuration Release
```

Click Load example to view the sample client and its three products. Enter the shared account password and click Load cloud folder to query real remote metadata for that example client. Installed-version values remain illustrative. Open installation file selects your own compatible file. An optional positional argument loads a specified file on startup:

```powershell
dotnet run --project desktop/Tytan.Updater.Desktop --configuration Release -- examples/installation.example.json
```

The compiled executable is desktop/Tytan.Updater.Desktop/bin/Release/net9.0-windows/Tytan.Updater.Desktop.exe. It requires the .NET 9 Windows Desktop Runtime when run without the SDK. A standalone distribution is not part of this first phase.

## Code organization

| File | Responsibility |
| --- | --- |
| Program.cs | Initialize WinForms and optional startup file; dispatch self-test mode |
| MainForm.cs | File selection, credentials, installed/cloud tabs, async queries, cancellation, errors |
| InstallationFileReader.cs | Parse and validate the provisional file into local installation models |
| CloudApiClient.cs | HTTPS BasicAuth query, JSON/path validation, deadline, cancellation |
| DesktopChecks.cs | Local file parsing and window checks |
| CloudChecks.cs | Simulated transport/window checks and optional real read-only verification |

## Verification

```powershell
dotnet build desktop/Tytan.Updater.Desktop --configuration Release
dotnet run --project desktop/Tytan.Updater.Desktop --configuration Release --no-build -- --self-test
```

The self-test briefly opens the actual window and checks the sample file, malformed JSON, folder traversal, malformed versions, duplicate/empty/null products, unexpected fields, rendered grid values, and preservation of valid data after an invalid replacement. The additional 17 cloud checks use an injected HTTP handler for query encoding/BasicAuth, metadata, HTTP failures, redirects, empty folders, validation, HTTPS, window states, cancellation, and timeout. The self-test makes no production requests.

## Optional real API verification

Supply TYTAN_API_USERNAME and TYTAN_API_PASSWORD privately to the process, then run:

```powershell
dotnet run --project desktop/Tytan.Updater.Desktop --configuration Release --no-build -- --verify-live
```

This briefly opens the window, queries the example client Barcin_Wodbar, verifies entries are displayed and installed versions stay unchanged, then closes. It never downloads ZIPs. Phase 2 successfully displayed three real production packages. Set TYTAN_DESKTOP_CAPTURE to an output PNG path to capture only this application's window during verification; the password remains masked.

## Next phases

1. Select the newest package independently for each installed product and compare numeric versions. Display Update available, Up to date, or No package.
2. Download selected newer ZIPs to an explicit destination; verify completion, sizes, and ZIP readability. A download does not change the recorded installed version.
3. Adapt the reader to the definitive local file and confirm how Tytan applies packages and maintains that file after successful installation.

Phases 1 and 2 deliver the local installation data and cloud listing. Comparison and downloading are the next separate phases.
