# Command-line updater

Updated: October 9, 2026.

The console accepts exactly two positional names: the cloud customer folder and the installed product folder. It extracts the product and version from the second name, compares with the server, and downloads the newest ZIP only if its version is higher. It opens no window, reads no local configuration file, and does not install packages.

## Arguments

```text
Tytan.Updater.Cli <client-folder> <installed-folder-name>
Tytan.Updater.Cli Barcin_Wodbar Faktury_008.000.042
```

- First argument: cloud customer folder, for example Barcin_Wodbar.
- Second argument: installed product folder name, for example Faktury_008.000.042.

The second argument is a name, not a path. Its format is product_NNN.NNN.NNN without a .zip extension. The last underscore separates the version, so product names can contain underscores. No JSON file or existing local folder is required. The supplied name is the source of the installed version; the CLI does not independently detect it.

ZIPs are saved under downloads/<client-folder> relative to the terminal's current working directory, for example downloads/Barcin_Wodbar/Faktury_008.000.043.zip. Existing ZIPs are preserved; an existing destination produces an error rather than being overwritten or considered installed.

## Run from PowerShell

From C:\Users\Ibai\tytan-updater, supply credentials to the current process environment. The password is requested without displaying it or putting its literal value in command history:

```powershell
$env:TYTAN_API_USERNAME = 'TytanSQL'
$secret = Read-Host 'Server password' -AsSecureString
$env:TYTAN_API_PASSWORD = [System.Net.NetworkCredential]::new('', $secret).Password
```

Then run with just the two folder names:

```powershell
dotnet run --project cli/Tytan.Updater.Cli --configuration Release -- Barcin_Wodbar Faktury_008.000.042
```

After testing, remove the password from the current environment:

```powershell
Remove-Item Env:TYTAN_API_PASSWORD
Remove-Variable secret
```

The default server is https://tytan.poznan.pl/SQLupdate/. The command requests api.php?dir=Barcin_Wodbar and, when a newer Faktury package is available, download.php?file=Barcin_Wodbar/Faktury_<new-version>.zip. Shared hosting credentials authenticate both requests. Choosing a folder does not restrict the shared account's server permissions.

## Output and exit codes

The command prints the product's installed version, available version, and comparison result. Transfers report progress and ZIP validation. Success prints the final ZIP path. If the server version is equal or older, it prints No updates available. Missing matching products and HTTP/authentication failures are reported as errors.

| Exit code | Meaning |
| --- | --- |
| 0 | No newer package, or the newer package downloaded successfully |
| 1 | Invalid folder name, missing package, authentication/network/validation error, timeout, or existing destination |
| 2 | Invalid number of command-line arguments |
| 130 | Cancelled with Ctrl+C |

Incomplete downloads are removed. Completed ZIPs are not extracted or installed. Repeating the same arguments compares the same supplied installed version; downloading does not change that version.

## Build, distribute, and test

```powershell
dotnet publish cli/Tytan.Updater.Cli --configuration Release --runtime win-x64 --self-contained true --output downloads/cli-publish
& ./downloads/cli-publish/Tytan.Updater.Cli.exe Barcin_Wodbar Faktury_008.000.042
```

Distribute the whole published directory. This console executable is separate from the WinForms program. Publishing with --self-contained true includes its .NET runtime.

```powershell
dotnet run --project cli/Tytan.Updater.Cli --configuration Release -- --help
dotnet run --project cli/Tytan.Updater.Cli --configuration Release -- --self-test
```

All 20 CLI checks use temporary local fixtures and an injected HTTP handler, never production. They cover equal/older/newer versions, authenticated listing/download, operation without local JSON or installed directories, existing ZIP preservation, invalid folder names and paths, underscores in product names, missing packages, authentication errors, missing password, invalid ZIP cleanup, cancellation, exactly two arguments, and help. The desktop application's JSON-based workflow is separate and unchanged.
