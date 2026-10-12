# Command-line updater

The Windows console compares the installed product version you supply with the newest matching ZIP in the customer's cloud folder. It downloads a newer package to the directory supplied as the third argument, or opens Save As when that argument is omitted. It does not extract or install updates, detect installed software, or change the installed version.

## Start from the extracted package

Extract the package matching your Windows architecture: win-x86 for 32-bit Windows or win-x64 for 64-bit Intel/AMD Windows. Open PowerShell inside the extracted directory containing `Tytan.Updater.Cli.exe`.

For example, when using the current x64 package in this repository:

```powershell
cd "C:\Users\Ibai\tytan-updater\dist\TytanUpdater-win-x64-20261012-093409-401bd4"
```

Adjust the path if you extracted the ZIP elsewhere. The executable includes its .NET runtime; the customer does not need the SDK or source repository. Run it from a terminal: double-clicking without arguments prints usage and closes.

## Configure the server and credentials

Copy the template beside the executable:

```powershell
Copy-Item .env.example .env
notepad .env
```

Edit the file as needed:

```dotenv
TYTAN_API_BASE_URL=https://tytan.poznan.pl/SQLupdate/
TYTAN_API_USERNAME=TytanSQL
# Optional: uncomment and replace the placeholder to save a password here.
# TYTAN_API_PASSWORD=your-server-password
```

The name must be `.env`, without a `.txt` extension. The program reads it from the executable's directory, independently of the terminal's working directory. URL and username have built-in defaults matching the example. The password must come from this file or a process environment variable. A password saved in `.env` is plain text; the program never writes this file. Only the template is included in distributable ZIPs.

To supply the password for the current PowerShell session instead of saving it in `.env`:

```powershell
$secret = Read-Host 'Server password' -AsSecureString
$env:TYTAN_API_PASSWORD = [System.Net.NetworkCredential]::new('', $secret).Password
```

Existing environment variables take priority over `.env`, followed by the built-in defaults. You can override the URL or username for the current session:

```powershell
$env:TYTAN_API_BASE_URL = 'https://updates.example.com/SQLupdate/'
$env:TYTAN_API_USERNAME = 'your-server-user'
```

Use the HTTPS directory containing both `api.php` and `download.php`, ending with `/`; do not include `api.php` in the configured URL. The server must provide the same endpoints, JSON contract, and BasicAuth behavior. HTTP, embedded credentials, queries, and fragments are rejected. Account changes are made on the hosting server, then reflected in the client credentials; recompilation is not required.

If a previous environment value is overriding your `.env`, remove the relevant variable before running again, for example:

```powershell
Remove-Item Env:TYTAN_API_BASE_URL -ErrorAction SilentlyContinue
```

Use UTF-8 and one `KEY=value` per line. Supported keys are `TYTAN_API_BASE_URL`, `TYTAN_API_USERNAME`, and `TYTAN_API_PASSWORD`. Blank lines and full-line `#` comments are accepted. Matching single or double quotes preserve spaces inside values. Characters such as `#` and `=` inside passwords are literal. Inline comments, escape expansion, and variable expansion are not supported. Invalid lines, duplicate/unknown keys, invalid UTF-8, and files exceeding 64 KiB produce configuration errors without printing their contents.

## Arguments

```text
Tytan.Updater.Cli.exe <client-folder> <installed-folder-name> [destination-folder]
```

| Argument | Meaning | Example |
| --- | --- | --- |
| First, required | Customer folder on the server | Barcin_Wodbar |
| Second, required | Installed product folder name containing its version | Faktury_008.000.042 |
| Third, optional | Existing local directory for the downloaded ZIP | C:\Tytan\Updates |

The second argument is a name, not a filesystem path. Use `product_NNN.NNN.NNN`, without `.zip`. The last underscore separates the version, so product names may contain underscores. No installation JSON or existing local installation folder is needed. The supplied version is used for comparison; ensure it matches the actual installed version.

### Download directly into a directory

Create the destination if needed, then run:

```powershell
New-Item -ItemType Directory -Path "C:\Tytan\Updates" -Force
.\Tytan.Updater.Cli.exe Barcin_Wodbar Faktury_008.000.042 "C:\Tytan\Updates"
```

For a newer server version, the ZIP is saved directly in that directory using its server filename, without a customer subfolder or a dialog. Quote paths containing spaces. Relative destinations resolve against the terminal's working directory. Missing directories and paths pointing to files are rejected before contacting the server. Existing destination files are never overwritten.

The destination parameter applies only to the console. It is not a `.env` setting.

### Choose the destination in a window

Omit the third argument:

```powershell
.\Tytan.Updater.Cli.exe Barcin_Wodbar Faktury_008.000.042
```

For a newer package, Save As suggests its ZIP filename. Choose the destination and click Save. This mode requires an interactive Windows session. Cancelling the dialog stops before requesting the ZIP.

## Results and exit codes

The command prints the installed version, available version, comparison result, download progress, and validation status. A successful download prints `ZIP saved to:` followed by the final path. If the installed version is equal or newer, it prints `No updates available.` and downloads nothing. A missing matching product package is an error.

| Exit code | Meaning |
| --- | --- |
| 0 | No newer package, or download completed successfully |
| 1 | Configuration, credentials, folder, network, timeout, ZIP validation, or destination error |
| 2 | Invalid number of arguments |
| 130 | Cancelled with Ctrl+C or Cancel in Save As |

Failed or cancelled transfers remove their incomplete temporary files. A completed download does not install Tytan or change the supplied installed version. Running again with the same version can therefore find the same update.

If PowerShell reports that `.\Tytan.Updater.Cli.exe` is not recognized, check that you are in the extracted directory containing the executable. If it reports `Set TYTAN_API_PASSWORD before running the command.`, supply the password through `.env` or the current terminal's environment.

After using a session password, remove it if desired:

```powershell
Remove-Item Env:TYTAN_API_PASSWORD -ErrorAction SilentlyContinue
Remove-Variable secret -ErrorAction SilentlyContinue
```

## Development and verification

From the repository root, with the .NET 9 SDK installed:

```powershell
dotnet run --project cli/Tytan.Updater.Cli --configuration Release -- Barcin_Wodbar Faktury_008.000.042 "C:\Tytan\Updates"
```

For `dotnet run`, supply credentials through the terminal environment or place `.env` in the project's executable output directory. The repository-root template is not loaded automatically.

From an extracted package:

```powershell
.\Tytan.Updater.Cli.exe --help
.\Tytan.Updater.Cli.exe --self-test
```

Self-test runs 16 shared configuration checks and 42 CLI checks using temporary local fixtures, without production requests. It skips the user's `.env`. Checks cover environment precedence, configuration parsing, custom endpoints, version comparison, direct and dialog destinations, existing-file protection, validation errors, and cancellation.

See [Windows distribution](distribution.md) for publishing self-contained 32-bit and 64-bit packages, and the [technical guide](technical-guide.md) for implementation details.
