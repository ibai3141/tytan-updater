# tytan-updater

PHP endpoints for distributing TytanSQL update ZIPs over HTTPS with BasicAuth. The delivered server lists available client folders and packages and streams a selected ZIP. Tytan's application selects versions and installs updates.

The repository delivers the PHP server and now starts a local Windows updater, following the clarified request to read client information and installed versions from the customer's computer. The earlier C# library/CLI prototype was removed; the new desktop application is developed incrementally under desktop/.

## Local Windows application: phases 1 to 4

The desktop automatically loads the customer and installed version configured for the current Windows user. Enter the shared account password and click Check for updates or press Enter. There are no folder-name inputs or example/import buttons. An administrator provisions the computer once; valid settings from the previous desktop version are reused. Missing or invalid settings show an administrator-setup message without listing other customers. Installed products shows Update available, Up to date, Installed version is newer, or No package. The Cloud folder tab retains the configured customer's metadata listing. Select an Update available row and click Download selected update to save its ZIP. Downloads show progress, support cancellation, preserve existing files, and validate size and ZIP readability. Installation is not performed, and installed versions are not detected automatically. See [desktop configuration](docs/desktop.md).

```powershell
dotnet run --project desktop/Tytan.Updater.Desktop --configuration Release
```

See [Desktop workflow, run instructions, and next phases](docs/desktop.md). All clients will use the shared hosting account, as requested; selecting a client folder is not an authorization boundary. The definitive installation-file format remains pending from the other developer.

## Command-line application

The Windows console executable accepts exactly two folder names: the cloud client folder and the installed product folder, such as `Faktury_008.000.042`. It extracts the product/version without reading a JSON file. If a newer ZIP is found, it opens Save As to choose where to save it. Otherwise, it prints `No updates available.` without opening a dialog. Cancelling Save As stops the download. It does not install updates.

```powershell
dotnet run --project cli/Tytan.Updater.Cli --configuration Release -- Barcin_Wodbar Faktury_008.000.042
```

Set the server credentials in the process environment first. See [CLI setup, arguments, credentials, output, and checks](docs/cli.md).

## Delivered files

For customer computers, build a portable Windows ZIP with `./scripts/publish_windows.ps1`. It includes self-contained desktop and console executables, the desktop example file, and end-user instructions; customers do not need the source repository or a separate .NET installation. See [Windows distribution](docs/distribution.md).

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
- [Windows executables and customer distribution](docs/distribution.md)
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
