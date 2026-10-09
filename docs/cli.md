# Command-line updater

Updated: October 9, 2026.

The console application checks a customer's cloud folder and downloads the newest ZIP for each locally recorded product when its version is higher. It reuses the desktop application's HTTPS client, numeric comparison, and validated download implementation. It opens no window and does not install packages or update the installed-version file.

## Arguments and provisional local format

```text
Tytan.Updater.Cli <client-folder> <local-version-folder> [--output <download-folder>]
```

1. `client-folder`: the exact cloud customer folder name, for example `Barcin_Wodbar`.
2. `local-version-folder`: an existing local folder containing `installation.json`.

The meaning and definitive contents of the requested second folder have not yet been confirmed. For now, `installation.json` uses the existing provisional [example format](../examples/installation.example.json). Copy that example under this filename to try the command. Its versions are test data, not detected installed versions. The first argument must match `clientFolder` in the file; a mismatch stops the command before HTTP access. All products in the file are checked.

By default, ZIPs are saved in `downloads` within the local version folder. `--output` selects another folder. Existing files are preserved; an existing destination ZIP produces an error rather than being overwritten or silently considered installed.

## Run from PowerShell

From `C:\Users\Ibai\tytan-updater`, prepare a local example:

```powershell
New-Item -ItemType Directory -Force downloads/cli-example
Copy-Item examples/installation.example.json downloads/cli-example/installation.json
```

Supply credentials to the current process environment. The password is requested without displaying it or putting its literal value in command history:

```powershell
$env:TYTAN_API_USERNAME = 'TytanSQL'
$secret = Read-Host 'Server password' -AsSecureString
$env:TYTAN_API_PASSWORD = [System.Net.NetworkCredential]::new('', $secret).Password
```

Run with the two positional parameters:

```powershell
dotnet run --project cli/Tytan.Updater.Cli --configuration Release -- Barcin_Wodbar "downloads/cli-example"
```

Select a separate output directory:

```powershell
dotnet run --project cli/Tytan.Updater.Cli --configuration Release -- Barcin_Wodbar "downloads/cli-example" --output "downloads/packages"
```

After testing, remove the password from the current environment:

```powershell
Remove-Item Env:TYTAN_API_PASSWORD
Remove-Variable secret
```

The default server is `https://tytan.poznan.pl/SQLupdate/`. The program requests `api.php?dir=<client-folder>` and downloads only selected newer packages with `download.php?file=<client-folder>/<zip-name>`. Shared hosting credentials authenticate both requests. Choosing a folder does not restrict the shared account's server permissions.

## Output and exit codes

Each product shows its installed version, available version, and comparison result. Transfers report progress and ZIP validation. A successful transfer prints the final ZIP path. A valid comparison without newer packages prints `No updates available.` Missing matching products and HTTP/authentication failures are reported as errors, not as an up-to-date installation.

| Exit code | Meaning |
| --- | --- |
| 0 | All requested comparisons succeeded; no newer packages, or all newer packages downloaded |
| 1 | Invalid local information, missing packages, authentication/network/validation error, timeout, or existing destination |
| 2 | Invalid command-line arguments |
| 130 | Cancelled with Ctrl+C |

If several products need updates, they download sequentially. An error stops subsequent downloads; previously completed ZIPs remain saved. Incomplete downloads are removed. Downloaded ZIPs are not installed; repeated runs compare the same installed versions until their real owner updates the local source.

## Build, distribute, and test

```powershell
dotnet publish cli/Tytan.Updater.Cli --configuration Release --runtime win-x64 --self-contained true --output downloads/cli-publish
& ./downloads/cli-publish/Tytan.Updater.Cli.exe Barcin_Wodbar "downloads/cli-example"
```

Distribute the whole published directory. This is a console executable separate from the WinForms program. Publishing with `--self-contained true` includes its .NET runtime.

```powershell
dotnet run --project cli/Tytan.Updater.Cli --configuration Release -- --help
dotnet run --project cli/Tytan.Updater.Cli --configuration Release -- --self-test
```

The CLI checks use temporary local fixtures and an injected HTTP handler, never production. They cover equal/older/newer versions, authenticated listing/download, unchanged installed metadata, existing ZIP preservation, customer mismatch, missing packages, authentication errors, missing password, invalid ZIP cleanup, cancellation, usage, and help.

The pending real-file adapter remains isolated in `Command.RunAsync`; replace its provisional `installation.json` read once the second parameter's agreed format is supplied.
