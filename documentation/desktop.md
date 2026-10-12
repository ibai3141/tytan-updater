# Local updater application

For customer delivery, see [Windows distribution](distribution.md). The publishing script creates a portable ZIP with self-contained desktop and console executables; customers do not need this repository or a separate .NET runtime installation.

## Phase 1: local window and installation file

Implemented:

- Windows Forms application targeting the available .NET 9 Windows runtime.
- Open a selected local JSON file or use the clearly labeled example.
- Show the client folder, product names, and installed versions.
- Validate all input before changing the displayed installation.
- Show English errors and preserve the last valid data on a failed replacement.
- Keep JSON mapping in InstallationFileReader so the final format can be substituted.

The desktop loads installed-version data, lists the configured cloud folder, compares versions, and downloads selected newer ZIPs. Installation is not implemented. Available-version and status cells show Not checked. Loading the example does not detect actual installations on this computer and does not modify the local file.

## Phase 2: HTTPS cloud-folder listing

Load an installation file or the example first. Enter the shared username and password, then click Check for updates. With the default base URL, the request is:

```text
https://tytan.poznan.pl/SQLupdate/api.php?dir=<loaded-clientFolder>
```

The application never fetches the root client list. Folder values are URL-encoded. The Cloud folder tab displays name, type, size in bytes, modification timestamp in UTC, and relative path. An empty folder is reported explicitly. Installed products and versions remain unchanged.

The password field is masked. The application does not write credentials to disk; it can read them from an optional `.env` beside the executable, where a user-supplied password is plain text. Existing process variables override `.env`. The username defaults to TytanSQL, and missing credentials can be entered in the window. See [server configuration](#server-configuration).

Local-file and credential controls are disabled during a query to prevent changing the client mid-request. Cancel stops the query; closing the window also cancels it. Each new query clears stale cloud results. Errors appear in the status label, leaving valid installed data unchanged; controls are restored afterward.

The 30-second deadline covers both headers and body reading. Automatic redirects are disabled, HTTPS is required, and standard certificate validation remains enabled. HTTP failures, malformed/non-JSON responses, invalid metadata, duplicate entries, and paths outside the loaded folder are rejected. Listings are limited to 8 MiB. The window also compares versions as described below.

Client-side folder selection remains the expressly requested shared-account design; it does not restrict other callers who have the same credentials.

## Phase 3: per-product version comparison

After a successful query, Installed products shows the selected available version and comparison result for every product in the local file. The Cloud folder tab still shows all returned entries.

Only ZIP files named Product_XXX.XXX.XXX.zip are candidates, with a full product prefix and three fixed-width numeric components. Selection is case-insensitive for the product and ZIP extension. Product names containing underscores are supported. Folder entries, malformed version names, unrelated products, and paths belonging to another client are ignored by selection. The API's metadata validation still applies first.

The largest major/minor/patch tuple is selected independently for each product. Modification dates and listing order do not determine which version is newest. Equal versions are not updates; an installed version higher than the newest package is explicitly identified so a downgrade is not suggested.

| Condition | Status |
| --- | --- |
| Available version is higher than installed | Update available |
| Available version equals installed | Up to date |
| Installed version is higher than available | Installed version is newer |
| No valid matching package exists | No package |

Rows with updates are highlighted. Missing packages show no available version. The summary counts updates without claiming that products with no package are up to date. Results are mapped by product name even if the user sorts the grid. Starting another query or loading another local file clears old comparisons; failed/cancelled refreshes cannot retain a previous update indication.

The installed versions and source file are never changed by comparison. The selected RemoteEntry is kept with the result and is now used by phase 4. Checking alone does not download or install a ZIP. The example-file warning remains visible after checking to distinguish illustrative installed versions from real installations.

Source: desktop/Tytan.Updater.Desktop/UpdateComparison.cs. This excerpt assumes a valid matching package has been selected:

```csharp
int comparison = latest.CompareTo(installed);
UpdateStatus status = comparison > 0 ? UpdateStatus.UpdateAvailable
    : comparison == 0 ? UpdateStatus.UpToDate : UpdateStatus.InstalledNewer;
results.Add(new ProductUpdate(product, selected, latest, status));
```

The real production listing, compared with the unchanged example installed versions, produced:

| Product | Installed example | Available on server | Status |
| --- | --- | --- | --- |
| Faktury | 008.000.042 | 008.000.043 | Update available |
| FK2025 | 005.005.007 | 005.005.007 | Up to date |
| FK2026 | 005.005.039 | 005.005.040 | Update available |

These installed values are still example data. Actual installation detection awaits the definitive local-file contract.

## Phase 4: download a selected update

1. Load your installation information (or the explicitly labeled example).
2. Enter credentials and click Check for updates.
3. In Installed products, select a row whose status is Update available.
4. Click Download selected update and choose a ZIP filename in an existing directory.
5. Wait for transfer and validation to complete. Only a validated ZIP saved at the final destination reaches 100%. The final Download complete status shows its path and explicitly states that automatic installation is unavailable. Click Open download folder to locate the saved ZIP.

Download is disabled for an equal, newer-installed, or missing package and while another operation runs. It is available only from the installed-product tab. The shared Cancel button and closing the window cancel an active download. File/credential/product controls remain disabled until it finishes.

The request uses HTTPS BasicAuth and download.php?file=<selected-relative-path>. Only a newer package in the loaded client's directory can be downloaded. Standard TLS validation and redirect rejection remain in force. A full HTTP 200 binary response is required; partial responses are rejected. Header retrieval uses the client's 30-second timeout, and the overall transfer plus validation has a 15-minute deadline.

The client streams bytes to a unique sibling .part file. Listed size, Content-Length when supplied, and actual received bytes must agree. Size changes require checking for updates again. Existing destination files are never overwritten, including a file created by another process during transfer.

After transfer, validation runs away from the UI thread. It opens the ZIP, requires at least one file, and reads its entries without extraction, checking their declared lengths. Expanded content is bounded at 2 GiB. This verifies ZIP structure/readability and completion; it is not a digital-signature, trusted checksum, or explicit CRC verification. Unsupported or unreadable archives cannot be published.

Only after validation and a final cancellation check does File.Move publish the ZIP without overwrite. Failures and cancellations remove the owned partial file; filesystem cleanup failures are reported rather than claiming successful completion. A completed file is not rolled back if cancellation arrives after publication. Progress stays below 100% during transfer and validation. While validating, the message says Transfer complete. Checking the ZIP before saving the final file. Only final publication reaches 100%; the Download complete status and Open download folder link identify the saved ZIP. A completed full bar remains visible as a completion indicator, not an installer activity indicator. Downloads are not resumed after interruption.

Neither the installed versions nor their source file is updated. No executable or installer is run and no archive is extracted. Applying the ZIP and recording successful installation still need an agreed Tytan integration contract.

## Download verification

The self-test includes 16 shared configuration checks, 10 local-file/window checks, 12 comparison checks, 22 cloud checks, and 23 download checks: 83 total. Download checks cover encoded endpoint/authentication, exact saved bytes, progress and validation, existing/concurrent destination preservation, cross-client and non-update rejection, HTTP errors/redirects/partial responses, header/body size mismatches, invalid ZIPs, interrupted streams, cancellation cleanup, UI completion and ZIP errors, and unchanged installed data.

Optional real verification requires credentials privately supplied in TYTAN_API_USERNAME and TYTAN_API_PASSWORD:

```powershell
dotnet run --project desktop/Tytan.Updater.Desktop --configuration Release --no-build -- --verify-live-download
```

This queries Barcin_Wodbar using the example installation, downloads Faktury to a newly owned temporary test directory, validates it, checks that installed information is unchanged, and removes the directory afterward. It does not leave a package for installation. Use the normal window to save a package permanently. The real check passed for Faktury_008.000.043.zip (17492922 bytes). No installation was performed.

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
cd "$env:USERPROFILE\tytan-updater"
dotnet run --project desktop/Tytan.Updater.Desktop --configuration Release
```

Click Load example to view the sample client and its three products. Enter the shared account password and click Check for updates to query real remote metadata for that example client. Installed-version values remain illustrative. Open installation file selects your own compatible file. An optional positional argument loads a specified file on startup:

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
| UpdateComparison.cs | Numeric versions, latest package selection, per-product results and statuses |
| ComparisonChecks.cs | Pure version/selection checks without network access |
| PackageDownload.cs | Streaming, progress, cancellation, size/ZIP validation, final publication |
| DownloadChecks.cs | Simulated failure and UI tests plus optional real download check |
| CloudChecks.cs | Simulated transport/window checks and optional real read-only verification |

## Verification

```powershell
dotnet build desktop/Tytan.Updater.Desktop --configuration Release
dotnet run --project desktop/Tytan.Updater.Desktop --configuration Release --no-build -- --self-test
```

The self-test first runs 16 isolated `.env` checks, then briefly opens the actual window and checks the sample file, malformed JSON, folder traversal, malformed versions, duplicate/empty/null products, unexpected fields, rendered grid values, and preservation of valid data after an invalid replacement. The 12 comparison checks cover numeric precedence, invalid formats, newest selection regardless of dates, product isolation, equal/newer/missing outcomes, empty listings, case, underscores, and unchanged installed data. The additional 22 cloud checks use an injected HTTP handler for query encoding/BasicAuth, metadata, HTTP failures, redirects, empty folders, validation, HTTPS, window states, cancellation, timeout, displayed comparisons, failed-refresh resets, sorted rows, and empty-folder status. The self-test makes no production requests.

## Optional real API verification

Supply TYTAN_API_USERNAME and TYTAN_API_PASSWORD privately to the process, then run:

```powershell
dotnet run --project desktop/Tytan.Updater.Desktop --configuration Release --no-build -- --verify-live
```

This briefly opens the window, queries the example client Barcin_Wodbar, verifies entries and comparison results are displayed while installed versions stay unchanged, then closes. It never downloads ZIPs. Phase 2 successfully displayed three real production packages; phase 3 also compared them and showed two updates against the example installed versions. Set TYTAN_DESKTOP_CAPTURE to an output PNG path to capture only this application's window during verification; the password remains masked.

## Server configuration

### Optional .env file beside the executables

New builds also read `.env` from the executable's directory, regardless of the terminal's working directory. Copy the included `.env.example` to `.env` and edit it:

```dotenv
TYTAN_API_BASE_URL=https://tytan.poznan.pl/SQLupdate/
TYTAN_API_USERNAME=TytanSQL
# Optional: uncomment only to store a password in plain text.
# TYTAN_API_PASSWORD=your-server-password
```

Restart the program after editing the file. Both programs use the same file when their executables are in the same directory. The desktop can then be started by double-clicking; enter the password in the window if it is omitted from `.env`. The console still requires a password from `.env` or `TYTAN_API_PASSWORD` in its environment. Its destination remains the optional third command-line argument; `.env` does not configure a download directory.

Precedence is: existing process environment, then `.env`, then the built-in URL/username defaults. If an old environment value overrides your file, remove that variable in the terminal or start the program from a session without it. Under `dotnet run`, the executable directory is the project's build output directory, not the repository root; the root `.env.example` is a template only and is not loaded automatically.

The supported keys are exactly `TYTAN_API_BASE_URL`, `TYTAN_API_USERNAME`, and `TYTAN_API_PASSWORD`. Use UTF-8, one `KEY=value` per line; blank lines and lines beginning with `#` are ignored. Optional matching single or double quotes preserve spaces inside values. `#`, `=`, backslashes, and dollar signs inside a value are literal. Inline comments and variable/escape expansion are not supported. Duplicate or unknown keys, malformed lines, unmatched opening quotes, control characters, invalid UTF-8, or files larger than 64 KiB produce a configuration error. The error identifies the line without printing its content. The file is parsed before any values are applied.

Only the credential-free `.env.example` belongs in Git and distributable ZIPs. Private `.env` and `.env.*` files are ignored, and the publisher never copies them. A password entered into `.env` is plain text; the program reads it but never writes or edits this file.

Both Windows applications read `TYTAN_API_BASE_URL` at startup. If it is unset, they use `https://tytan.poznan.pl/SQLupdate/`. To change the server without rebuilding:

```powershell
$env:TYTAN_API_BASE_URL = 'https://updates.example.com/SQLupdate/'
```

Use the HTTPS directory containing both `api.php` and `download.php`, with a trailing slash. Do not enter the full `api.php` URL. HTTP, embedded credentials, query strings, fragments, and invalid URLs are rejected. Restart the application after changing its environment. To restore the default, run `Remove-Item Env:TYTAN_API_BASE_URL` in the same terminal before starting it again. These commands configure only that terminal and applications launched from it.

Credentials are configured separately. Set `TYTAN_API_USERNAME` and `TYTAN_API_PASSWORD` for the console; the desktop also allows entering them in its fields. The administrator changes the actual account on the hosting server. Changing credentials does not require rebuilding. The new server must expose the same endpoint names, JSON contract, and BasicAuth behavior; a URL change alone cannot adapt a different API.
