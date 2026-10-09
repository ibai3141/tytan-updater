# Verification record

Current status: the user has confirmed working production listing and downloading. The PHP server passed 37 endpoint checks on PHP 7.2.34 and PHP 8.5.11 after the initial C# prototype was removed. A later request adds a new incremental local Windows application, recorded below. Earlier sections preserve historical checkpoints.

## October 5, 2026: live server query

A GET request with BasicAuth was sent to:

```text
https://tytan.poznan.pl/SQLupdate/api.php?dir=Barcin_Wodbar
```

Result: **HTTP 404**. No packages were downloaded. The request did not follow redirects, and credentials were read from the original document without storing them in the repository. A subsequent query using the implemented CLI also returned HTTP 404.

At that time, the working assumption was that endpoints were already published. On October 6, the user corrected this: the project must create them. The historical 404 does not validate the new implementation or identify its original cause.

This section records historical evidence. Subsequent production confirmation and the current PHP-only scope are recorded below.

## Local implementation validation

- Release build with .NET SDK 9.0.304: zero errors and zero warnings.
- Test executable: 15 of 15 cases passed. Coverage includes comparison, JSON, HTTP errors, complete downloads, invalid ZIPs, size mismatches, interruptions, cancellation, cross-client paths, destination preservation, and concurrency.
- CLI: a complete demo returned `Downloaded`; repeating it returned `Error` and exit code 1 while preserving the existing ZIP; help returned exit code 0.
- The demo ran in a temporary folder, and its sample package was removed afterward.
- Whitespace and patch formatting were checked using `git diff --check`.
- After translating console messages into English, all 15 tests passed again, and the demo printed `Package downloaded; Tytan can proceed with installation.`

Tytan's actual application is not in the repository: an integration example has been delivered, rather than integration executed inside its program. No live download has been tested because the documented listing endpoint returned 404.

## October 6, 2026: actual PHP and C# integration

- Scope corrected: this project creates api.php and download.php; they were not an existing dependency to assume published.
- Portable PHP 8.5.11 was downloaded from the official PHP distribution and its SHA-256 was verified. It was used only in a temporary directory for local checks.
- PHP syntax checks passed for api.php, download.php, and common.php.
- 36 actual-PHP checks passed, including authentication, listings, empty directories, malformed parameters, traversal, sibling-prefix containment, downloads, byte equality, headers, HEAD requests, HTTP rejection, missing credentials, and invalid package-root configuration.
- The filesystem symlink escape test was skipped because the Windows environment did not allow creating symlinks. The direct path-containment boundary check passed.
- 17 C# tests passed: the existing 15 plus actual-PHP listing and package download/no-update integration.
- The .NET Release build completed with zero errors and zero warnings.
- Local PHP used its built-in server on loopback HTTP. A test-only handler connected the C# client to it. Production HTTPS transport, Apache BasicAuth, and hosting publication are not validated by these checks.
- No production files were uploaded or changed. Deployment remains pending; see [Server setup](server.md).

## October 6, 2026: hosting PHP 7.2 compatibility fix

- The user reported uploading the endpoint files. A read-only production check returned HTTP 401 without credentials and HTTP 500 with the generic endpoint JSON error after authentication.
- The user confirmed PHP 7.2.34 and that str_starts_with is unavailable. The original common.php also used JSON_THROW_ON_ERROR, introduced after PHP 7.2.
- common.php now checks prefixes using strpos, explicitly handles json_encode failures, and logs exception details privately while keeping generic JSON responses.
- All three files passed syntax checks on actual PHP 7.2.34 and PHP 8.5.11. All 37 endpoint checks passed on each runtime, including a new invalid UTF-8 encoding failure and private-log regression check.
- All 17 C# tests passed against PHP 7.2.34, including actual listing and ZIP download integration. The filesystem symlink case remains skipped because Windows does not allow creating it in this environment.
- The compatibility fix has not been uploaded by this agent. Replace the deployed common.php and verify the live listing and download. Production success is not yet confirmed.

## October 6, 2026: user-confirmed production results

After replacing common.php, the user supplied the successful root response:

| Name | Type | Size | Modified (UTC) | Path |
| --- | --- | --- | --- | --- |
| Barcin_Wodbar | folder | null | 2026-10-02 06:57:35 | Barcin_Wodbar |
| barczewo_zwik | folder | null | 2026-10-02 06:57:44 | barczewo_zwik |

The user then supplied api.php?dir=Barcin_Wodbar with these exact entries:

| Name | Type | Size (bytes) | Modified (UTC) | Path |
| --- | --- | --- | --- | --- |
| FK2025_005.005.007.zip | file | 11006463 | 2026-10-02 06:57:35 | Barcin_Wodbar/FK2025_005.005.007.zip |
| FK2026_005.005.040.zip | file | 26190268 | 2026-10-02 06:57:38 | Barcin_Wodbar/FK2026_005.005.040.zip |
| Faktury_008.000.043.zip | file | 17492922 | 2026-10-02 06:57:29 | Barcin_Wodbar/Faktury_008.000.043.zip |

The user subsequently stated that everything works after the download instructions. Download success is therefore user-reported. The agent has not independently downloaded these production packages or measured their checksum, actual downloaded size, archive integrity, or concurrent performance. The production listing sizes above are exact user-provided metadata, not local test fixtures.

The observed initial 500 was resolved after the PHP 7.2 compatibility fix. Root and client listing now work according to supplied responses. The earlier deployment-pending notes describe the state at their respective historical checkpoints.

## Current PHP-only delivery

At the user's request, src, the C# solution, dependent C# tests, and the optional client test switch were removed. They remain in Git history. Historical 17/17 C# results above describe the earlier implementation, not a test command available in the current checkout.

The retained Python harness tests the three actual PHP files independently of .NET. It verifies 37 endpoint checks on PHP 7.2.34 and PHP 8.5.11 in this Windows environment; the filesystem symlink case is skipped when creating symlinks is unavailable. Tytan's actual version selection and installation remain outside the acceptance performed here.

## October 6, 2026: new desktop phase 1

- Scope clarified: create a local window to read the client identifier and installed versions, query that client's cloud directory, compare versions, and download newer packages. Shared credentials and client-side directory selection were expressly requested; no customer access isolation is claimed.
- The repository was clean before this phase. The uncommitted Swagger page, specification, scripts, and README addition were already absent; only an empty leftover resource directory remained. No Swagger work is included in this delivery.
- A new WinForms application targets net9.0-windows under desktop/. It reads a provisional JSON file rather than claiming knowledge of the definitive file or detecting actual installed applications.
- Release build succeeded with zero errors and zero warnings using .NET SDK 9.0.304.
- All 10 desktop checks passed: example parsing, malformed JSON, traversal, malformed versions, duplicate/empty/null products, unexpected fields, window grid values, and preserving valid state after an invalid replacement.
- The actual form was opened by the smoke check and captured via DrawToBitmap for visual inspection. The capture contains only this application's window and is stored in ignored downloads/.
- No API requests, production changes, downloads, installations, credential persistence, or installation-file writes occur in this phase. PHP endpoint behavior was not changed.

## October 6, 2026: desktop phase 2 cloud listing

- Added asynchronous HTTPS BasicAuth access to api.php?dir=<loaded-clientFolder>, with no root-client enumeration. PHP endpoints were not changed.
- The window has masked in-memory credentials, Installed products and Cloud folder tabs, loading state, cancellation, and English status errors. Credential/files controls are disabled while querying. No password is embedded or saved.
- Release build passed without errors or warnings. All 10 previous local checks and 17 new simulated cloud checks passed, covering encoded query names, BasicAuth, metadata, HTTP 401/404/500, redirect rejection, invalid JSON/path/metadata, empty folders, invalid input, HTML responses, HTTPS, window results, preservation of installed versions, busy state, cancellation, and timeout.
- A real read-only verification queried https://tytan.poznan.pl/SQLupdate/api.php?dir=Barcin_Wodbar through the new desktop client. Credentials were read privately from the original documents and supplied only to the verification process; they were not printed or stored in the repository.
- The window displayed FK2025_005.005.007.zip (11006463 bytes), FK2026_005.005.040.zip (26190268 bytes), and Faktury_008.000.043.zip (17492922 bytes), with UTC timestamps and relative paths matching the server listing.
- Application-only screenshot inspected locally; password was masked. Installed sample versions remained unchanged. No ZIP was downloaded or installed, and no production file or setting was modified.
- Package comparison and downloading remain the next phases; the real local-file format is still pending.

## October 6, 2026: desktop phase 3 version comparison

- Added PackageVersion and UpdateComparison to select the latest valid product ZIP numerically and compare against the local installed version. Dates do not select versions; unrelated products, folders, malformed filenames, and cross-client entries are excluded.
- Installed products now shows available versions and Update available, Up to date, Installed version is newer, or No package. Sorting the grid preserves the mapping between a product and its result. Refreshes clear old comparisons, including on errors and cancellation. Example installed data remains explicitly labeled.
- Release build passed with zero errors and warnings. All 44 checks passed: 10 local installation/window checks, 12 comparison checks, and 22 simulated transport/window checks. New cases include version precedence, product isolation, missing/equal/newer outcomes, unchanged local data, sorted-row results, and empty-folder behavior.
- A real read-only API query through the desktop application successfully compared the production packages with the example installed versions: Faktury 008.000.042 -> 008.000.043 and FK2026 005.005.039 -> 005.005.040 are updates; FK2025 005.005.007 equals 005.005.007 and is up to date. The example versions are not actual installation detection.
- The application-only screenshot was visually inspected. Credentials were supplied privately from the original documents and stayed masked; no credential was committed. No ZIP was downloaded or installed, no local installation file was modified, and PHP endpoints were unchanged.
- Downloading selected newer packages is the next phase. The definitive local installation-file format remains pending.

## October 7, 2026: desktop phase 4 package download

- Added Download selected update and a progress bar to the window. Only newer comparison results from the loaded client can be saved. Download/cancellation/error flow shares the existing busy controls, leaving installed information unchanged.
- Binary HTTPS response streams into an owned unique .part file. Listed/header/received sizes are checked; ZIP entries are read without extraction before non-overwriting publication. Existing files, including a destination created during transfer, are preserved. Temporary files are cleaned on failures and cancellation.
- Validation has a 2 GiB expanded-content bound and is run outside the UI thread. Overall deadline is 15 minutes. It checks completion and ZIP readability, not cryptographic authenticity or explicit CRC integrity. No installation or source-file write is performed.
- InvalidDataException is now explicitly handled in the local-file, cloud-query, and download UI error paths. This prevents validation errors escaping the async UI handlers.
- Release build passed with zero errors and warnings. All 65 checks passed: 10 local, 12 comparison, 22 cloud, and 21 download checks, covering the successful path, progress, HTTP/redirect/partial responses, header and body mismatches, invalid ZIPs, interruption, cancellation, cross-client/non-update rejection, destination races, and UI validation-error recovery.
- Real verification queried the production API and downloaded Faktury_008.000.043.zip (17492922 listed bytes) through download.php using the desktop code. Size checks and ZIP readability passed. The installed example version stayed 008.000.042 and its file was unchanged. The owned temporary test directory was removed afterward.
- Credentials were read privately from the original documents and supplied only to the verification child process. Screenshot contained only the application window with a masked password. No server files/settings or production packages were modified; no downloaded executable was run.
- Remaining work depends on the other developer's definitive installation file and Tytan's installation contract. This phase delivers downloading, not automatic installation.

## October 8, 2026: clarify download completion

- The user reported a full progress bar and no installation. The exact message in their window was requested; the reported case has not been independently diagnosed from that message.
- Code inspection confirmed that transferred bytes previously reached 100% before ZIP validation and final publication. Progress now stays at most 99% until the verified ZIP has been published. Validation has an explicit status message.
- Successful download now displays Download complete, its saved ZIP path, and Automatic installation is not available. Open download folder opens the containing directory, never executes the package. The full bar remains visible after successful completion.
- Release build passed with zero errors/warnings. All 67 checks passed, including new checks for progress before/after publication and the completed window's saved path and installation explanation.
- A fresh real Faktury download through the desktop code passed size/readability checks and completed in the window. The application-only screenshot was inspected. The verification package was saved in its owned temporary test directory and removed afterward; normal user downloads remain at the selected destination.
- Automatic installation remains unimplemented pending the installer contract. No installed-version file or server configuration was changed.

## October 9, 2026: console application

- Added a separate console executable with two positional arguments: cloud customer folder and local version folder. The second provisionally contains installation.json; its agreed format is still pending confirmation.
- Reused the desktop HTTP, comparison, and ZIP download sources without changing them. No window is opened by the CLI.
- All 12 local CLI checks passed: equal/older/newer versions, authenticated request paths and ZIP bytes, unchanged installed metadata, existing destination preservation, customer mismatch before HTTP, missing package handling, authentication failure, missing password, invalid ZIP cleanup, cancellation, usage, and help.
- The compiled console executable's --help output was checked directly in PowerShell. The existing desktop Release build passed with zero warnings and errors.
- These new checks use local temporary fixtures and an injected HTTP handler. No new production download was performed for this change. Earlier real production download evidence remains recorded above.

### Corrected two-name CLI contract

- The user clarified that the second argument is the installed folder's name, not a local directory containing JSON. This supersedes the initial provisional CLI contract above.
- The CLI now accepts exactly two positional names, for example `Barcin_Wodbar Faktury_008.000.042`. It derives the product and version from the last underscore; it reads no local JSON and requires no existing installed directory. ZIPs are saved in `downloads/<client-folder>` under the current working directory.
- All 20 revised CLI checks passed, including operation without a JSON file or installed directory, invalid version/name/path rejection before HTTP, product names containing underscores, and rejection of extra arguments. Existing comparison, authentication, cancellation, and validated-download checks remain covered.
- No production request or server change was needed for this correction. The desktop application's separate JSON workflow is unchanged.

### User-selected CLI destination

- Added an optional third positional argument for the exact download directory. Explicit paths do not have a customer subfolder appended; the existing two-argument command retains its default downloads/<client-folder> destination.
- All 24 CLI checks passed. New checks verify successful downloading to a newly created directory with spaces, empty-path rejection before HTTP, no directory creation when up to date, and preservation/error reporting when the destination is an existing file.
- Documentation and help now show the third argument. These checks used local fixtures only; no production request or server change was needed.

### Final destination interaction: Save As with two arguments

- The user clarified that destination selection must happen in a window, with only two command-line parameters. This supersedes the third-argument destination described above.
- The Windows console now opens Save As only after selecting a newer package, suggests that ZIP's filename, and downloads directly to the chosen full path. Cancelling the dialog returns 130 before any ZIP request. Equal/older server versions and authentication failures do not open the dialog. Existing files remain protected.
- All 24 revised CLI checks passed using injected HTTP responses and a destination chooser, including dialog cancellation, cancellation after selection, the selected path with spaces, and rejection of a third argument.
- An additional temporary local harness invoked the actual production SaveFileDialog method, observed its Save update ZIP window on the dedicated STA thread, closed that process's dialog, and verified that it returned cancellation. This verifies real dialog opening/cancellation; successful saved-path downloading is covered by the injected chooser checks.
- The CLI now targets net9.0-windows with Windows Forms while retaining console output. No production request, server change, or installation was performed. Instructions and help use only two parameters.

### Portable Windows executable delivery

- Added scripts/publish_windows.ps1 to publish both applications as self-contained single-file executables with native-library self-extraction, no trimming, and no debug symbols. The script creates unique staging/release directories and a ZIP, reports its SHA256, and stops on publishing failure. Credentials are not packaged.
- Generated dist/TytanUpdater-win-x64-20261009-094714-e05a10.zip, approximately 85.3 MiB. It contains exactly Tytan.Updater.Desktop.exe, Tytan.Updater.Cli.exe, installation.example.json, and README.txt. Build staging and dist artifacts are ignored by Git; source publishing instructions are committed.
- SHA256: 29A0E00943D989D0300CA11FE0AC78F8FB306BEB03E90B88733000A2A5F99D7E.
- Extracted the ZIP into a new isolated directory and invoked the published console help, all 24 CLI checks, and all 67 desktop checks successfully. DOTNET_ROOT/DOTNET_ROOT_X64 pointed to an absent runtime directory and multilevel lookup was disabled in the child-process environment. No source files were needed in that extracted directory.
- These checks ran on the development computer using local fixtures, without production requests. They do not replace acceptance on a representative customer computer or validate ARM64 hardware. Only win-x64 was built and checked in this delivery.
- The interface still uses provisional installation JSON, while the console accepts two folder names and prompts for a save location. Neither application installs Tytan update packages.
