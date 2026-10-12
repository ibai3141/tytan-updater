# Command-line updater

Updated: October 12, 2026.

The Windows console accepts two required names and an optional destination directory: the cloud customer folder and the installed product folder. It extracts the product and version from the second name and compares with the server. For a newer ZIP, it uses the destination parameter when supplied; otherwise it opens Save As. It then downloads and validates the package. It reads no installation JSON file and does not install packages.

## Arguments

```text
Tytan.Updater.Cli <client-folder> <installed-folder-name> [destination-folder]
Tytan.Updater.Cli Barcin_Wodbar Faktury_008.000.042
```

- First argument: cloud customer folder, for example Barcin_Wodbar.
- Second argument: installed product folder name, for example Faktury_008.000.042.

The second argument is a name, not a path. Its format is product_NNN.NNN.NNN without a .zip extension. The last underscore separates the version, so product names can contain underscores. No installation JSON file or existing local installation folder is required. The supplied name is the source of the installed version; the CLI does not independently detect it.

When a newer version is found, Save As suggests the available package's ZIP filename. Choose the folder and click Save. The chosen full path is used directly, without a customer subfolder. Cancelling the dialog stops before requesting ZIP data. If the installed version is equal or newer, no dialog opens. Existing ZIPs are preserved; selecting an existing file produces an error rather than overwriting it.

### Download without a dialog

The optional third argument is an existing local destination directory, not a ZIP filename:

```powershell
.\Tytan.Updater.Cli.exe Barcin_Wodbar Faktury_008.000.042 "C:\Tytan\Updates"
```

Create the directory first and ensure the account can write to it. Use quotes around paths containing spaces. Relative paths resolve against the terminal's current working directory. A newer package is saved directly as `<destination-folder>/<server-package-name>.zip`, without a customer subfolder or Save As dialog. An empty, missing, or file-valued destination is rejected before contacting the server. Existing ZIPs are preserved. If no newer version exists, the command prints `No updates available.` and downloads nothing.

Omit the third argument to keep the existing Save As workflow. Both modes download and validate ZIPs; neither installs them.

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

Distribute the whole published directory. This console executable is separate from the main desktop window, and requires an interactive Windows session only when using Save As. The third-argument mode opens no window. It targets net9.0-windows and uses Windows Forms for the dialog. Publishing with --self-contained true includes its .NET runtime.

```powershell
dotnet run --project cli/Tytan.Updater.Cli --configuration Release -- --help
dotnet run --project cli/Tytan.Updater.Cli --configuration Release -- --self-test
```

All 42 CLI checks use temporary local fixtures, an injected HTTP handler and an injected destination chooser, never production. They cover equal/older/newer versions, authenticated listing/download, operation without local JSON or installed directories, existing ZIP preservation, invalid names and paths, product names containing underscores, missing packages, authentication errors, missing password, invalid ZIP cleanup, cancellation, two or three arguments, configurable API URLs, and help. Chooser checks cover use of the selected path with spaces, no dialog without an update or after authentication failure, cancelling before download, and cancellation immediately after choosing a path. Additional checks cover direct destinations, relative paths, custom listing/download URLs, environment configuration, invalid URLs, and invalid ZIP cleanup without a dialog. The desktop application's JSON-based workflow is separate.

DownloadLocation runs SaveFileDialog on a dedicated STA thread because console HTTP continuations run on thread-pool threads. Command.RunAsync invokes it only after comparison selects a newer ZIP. Ctrl+C cancels the pending wait; the background dialog thread does not keep the console process alive.
