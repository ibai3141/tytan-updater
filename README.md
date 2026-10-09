# tytan-updater

PHP endpoints for distributing TytanSQL update ZIPs over HTTPS with BasicAuth. The delivered server lists available client folders and packages and streams a selected ZIP. Tytan's application selects versions and installs updates.

The repository delivers the PHP server and now starts a local Windows updater, following the clarified request to read client information and installed versions from the customer's computer. The earlier C# library/CLI prototype was removed; the new desktop application is developed incrementally under desktop/.

## Local Windows application: phases 1 to 4

The desktop application opens a local installation JSON file and displays the client folder, products, and installed versions. Click Load example to try the provisional format, enter the shared account credentials, then click Check for updates. It queries that client's API directory and compares the newest valid ZIP version for each installed product. Installed products shows Update available, Up to date, Installed version is newer, or No package. The Cloud folder tab retains the full metadata listing. Select an Update available row and click Download selected update to save its ZIP. Downloads show progress, support cancellation, preserve existing files, and validate size and ZIP readability before completion. Installation is not performed.

```powershell
dotnet run --project desktop/Tytan.Updater.Desktop --configuration Release
```

See [Desktop workflow, run instructions, and next phases](docs/desktop.md). All clients will use the shared hosting account, as requested; selecting a client folder is not an authorization boundary. The definitive installation-file format remains pending from the other developer.

## Command-line application

The separate console executable accepts two required folder names: the cloud client folder and the installed product folder, such as `Faktury_008.000.042`. An optional third argument selects the exact download directory. It extracts the product/version from the installed folder name without reading a JSON file. It downloads a newer ZIP into the chosen directory (default: `downloads/<client-folder>` under the current directory), or prints `No updates available.` when the server version is equal or older. It does not install updates.

```powershell
dotnet run --project cli/Tytan.Updater.Cli --configuration Release -- Barcin_Wodbar Faktury_008.000.042 "C:/Users/Ibai/Downloads/Tytan"
```

Set the server credentials in the process environment first. See [CLI setup, arguments, credentials, output, and checks](docs/cli.md).

## Delivered files

| File | Purpose |
| --- | --- |
| server/api.php | JSON listing of folders and ZIP packages |
| server/download.php | Download an existing ZIP by relative path |
| server/common.php | Shared authentication, HTTPS, paths, and errors |
| server/.htaccess.example | Optional Apache configuration reference |
| tests/server/test_endpoints.py | Automated local endpoint checks |

Deploy the three PHP files to SQLupdate. Keep the hosting's working authentication and HTTPS configuration. The Apache example requires adaptation and must not overwrite the existing configuration blindly.

## Documentation

- [Technical documentation with source excerpts](docs/technical-walkthrough.md)
- [Technical documentation in Word](docs/technical-walkthrough.docx)
- [Server deployment and configuration](docs/server.md)
- [URL usage and acceptance checks](docs/uso.md)
- [Scope and integration contract](docs/planteamiento.md)
- [Implementation phases](docs/fases.md)
- [Results and verification history](docs/verificacion.md)
- [Local Windows application](docs/desktop.md)
- [Command-line updater](docs/cli.md)
- [Installation integration contract and pending inputs](docs/installation-contract.md)

## Production URLs

```text
https://tytan.poznan.pl/SQLupdate/api.php
https://tytan.poznan.pl/SQLupdate/api.php?dir=Barcin_Wodbar
https://tytan.poznan.pl/SQLupdate/download.php?file=Barcin_Wodbar/Faktury_008.000.043.zip
```

On October 6, 2026, the user supplied successful root and client JSON listings and confirmed that downloading also works. The hosting runtime is PHP 7.2.34. Local tests passed on PHP 7.2.34 and PHP 8.5.11. See the verification record for exact evidence and remaining acceptance work.

## Local tests

The PHP endpoint tests require Python and PHP; no .NET SDK, Composer, or database is needed for those tests. The desktop build and checks separately require the .NET 9 SDK on Windows.

```powershell
python tests/server/test_endpoints.py --php C:/path/to/php.exe
```

The harness creates temporary ZIP fixtures and local PHP servers, validates responses and downloads, then stops the servers. It never contacts production.

## Regenerate the Word document

```powershell
python -m pip install -r scripts/requirements-docs.txt
python scripts/export_technical_doc.py
```

Credentials are configured privately outside the repository. Only server PHP files belong on the hosting server.
