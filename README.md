# tytan-updater

A C# module for TytanSQL that checks for updates over HTTPS, compares versions, and downloads the latest ZIP when it is newer than the installed version. Tytan handles installation.

The repository contains a .NET 9 library, a command-line tool, and local tests without external dependencies.

## Quick start

```powershell
dotnet build Tytan.Updater.sln --configuration Release
dotnet run --project tests/Tytan.Updater.Tests --configuration Release
dotnet run --project src/Tytan.Updater.Cli -- demo ./downloads/demo
```

The demo creates a sample ZIP without contacting the server. See [Usage and integration](docs/uso.md) for credentials, listing files, downloading packages, and calling the module from Tytan.

## Confirmed scope

- C# integration with TytanSQL using HttpClient.
- Server base URL: `https://tytan.poznan.pl/SQLupdate/`.
- BasicAuth on every HTTPS request.
- `api.php` lists files and folders as JSON.
- `api.php?dir=<folder>` lists a client's folder.
- `download.php?file=<relative-path>` downloads a file.
- The user confirms both endpoints are already published.
- Each client's folder contains packages for the products they own.
- Tytan supplies the client folder, product, and installed version.
- The module returns the downloaded ZIP's local path and version.
- Tytan installs the package and manages the installed version.

## Example

```text
Client folder:     Barcin_Wodbar
Product:           Faktury
Installed version: 008.000.042
Remote package:    Faktury_008.000.043.zip
Result:            ZIP downloaded; local path returned to Tytan
```

## Implementation workflow

1. Receive Tytan's data through the C# interface.
2. Query the API and deserialize JSON into objects.
3. Filter ZIP packages by product and compare numeric versions.
4. Download to a temporary file and publish it locally when complete.
5. Return an explicit result: no update, downloaded, cancelled, or error.
6. Verify the rules locally, then check the actual server contract.

The library targets the available .NET 9 environment. Compatibility with Tytan's application must be checked during integration.

See [Technical design](docs/planteamiento.md) for the contract, endpoints, and rules, and [Implementation phases](docs/fases.md) for deliverables and commits.

## Sources and status

The documents reviewed in `F:\SQL_Update` include project v1.0, project v1.1, the HTTPS + BasicAuth integration guide, and its backup. The scope reflects the user's confirmations on October 5, 2026.

The user confirms the endpoints are published. The live request to `api.php?dir=Barcin_Wodbar` on October 5 returned HTTP 404, so the published path still needs to be checked. See [Verification record](docs/verificacion.md). Credentials are configured outside the repository.
