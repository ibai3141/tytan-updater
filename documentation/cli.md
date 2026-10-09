# Command-line updater

Updated: October 9, 2026.

The Windows console accepts exactly two names: the cloud customer folder and the installed product folder. It extracts the product and version from the second name and compares with the server. Only when a newer ZIP is available does it open a Windows Save As dialog to choose the destination, then download and validate the package. It reads no local configuration file and does not install packages.

## Arguments

```text
Tytan.Updater.Cli <client-folder> <installed-folder-name>
Tytan.Updater.Cli Barcin_Wodbar Faktury_008.000.042
```

- First argument: cloud customer folder, for example Barcin_Wodbar.
- Second argument: installed product folder name, for example Faktury_008.000.042.

The second argument is a name, not a path. Its format is product_NNN.NNN.NNN without a .zip extension. The last underscore separates the version, so product names can contain underscores. No JSON file or existing local folder is required. The supplied name is the source of the installed version; the CLI does not independently detect it.

When a newer version is found, Save As suggests the available package's ZIP filename. Choose the folder and click Save. The chosen full path is used directly, without a customer subfolder. Cancelling the dialog stops before requesting ZIP data. If the installed version is equal or newer, no dialog opens. Existing ZIPs are preserved; selecting an existing file produces an error rather than overwriting it.

## Run from PowerShell

From C:\Users\Ibai\tytan-updater, supply credentials to the current process environment. The password is requested without displaying it or putting its literal value in command history:

```powershell
$env:TYTAN_API_USERNAME = 'TytanSQL'
$secret = Read-Host 'Server password' -AsSecureString
$env:TYTAN_API_PASSWORD = [System.Net.NetworkCredential]::new('', $secret).Password
```

Then run with only the two folder names; choose the destination in the window if an update is available:

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
| 130 | Cancelled with Ctrl+C or Cancel in Save As |

Incomplete downloads are removed. Completed ZIPs are not extracted or installed. Repeating the same arguments compares the same supplied installed version; downloading does not change that version.

## Build, distribute, and test

For a customer delivery containing both self-contained executables and instructions, use the [Windows distribution script](distribution.md). The command below publishes the console separately.

```powershell
dotnet publish cli/Tytan.Updater.Cli --configuration Release --runtime win-x64 --self-contained true --output downloads/cli-publish
& ./downloads/cli-publish/Tytan.Updater.Cli.exe Barcin_Wodbar Faktury_008.000.042
```

Distribute the whole published directory. This console executable is separate from the main desktop window, but requires an interactive Windows session for Save As. It targets net9.0-windows and uses Windows Forms for the dialog. Publishing with --self-contained true includes its .NET runtime.

```powershell
dotnet run --project cli/Tytan.Updater.Cli --configuration Release -- --help
dotnet run --project cli/Tytan.Updater.Cli --configuration Release -- --self-test
```

All 24 CLI checks use temporary local fixtures, an injected HTTP handler and an injected destination chooser, never production. They cover equal/older/newer versions, authenticated listing/download, operation without local JSON or installed directories, existing ZIP preservation, invalid names and paths, product names containing underscores, missing packages, authentication errors, missing password, invalid ZIP cleanup, cancellation, exactly two arguments, and help. Chooser checks cover use of the selected path with spaces, no dialog without an update or after authentication failure, cancelling before download, and cancellation immediately after choosing a path. The desktop application's JSON-based workflow is separate and unchanged.

DownloadLocation runs SaveFileDialog on a dedicated STA thread because console HTTP continuations run on thread-pool threads. Command.RunAsync invokes it only after comparison selects a newer ZIP. Ctrl+C cancels the pending wait; the background dialog thread does not keep the console process alive.
