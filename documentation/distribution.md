# Windows distribution

The user receives a ZIP containing two self-contained executables, a provisional example installation file, a credential-free `.env.example`, and English usage instructions. Source code, PHP files, credentials, the .NET SDK, and build directories are not included. No separate .NET runtime installation is required.

## October 12 .env delivery (current)

Use these builds to load the optional executable-side `.env`. The ZIP contains only a safe `.env.example` template; no private configuration or password is included.

| Architecture | ZIP | SHA256 |
| --- | --- | --- |
| 32-bit Windows | TytanUpdater-win-x86.zip | 3550D3E949A2CB444F224834EEC05019C7E1FE5D70D61AD1DA9892005BA9C3CB |
| 64-bit Intel/AMD Windows | TytanUpdater-win-x64.zip | 68F342C8924DDB32CEE093231DFA21FCE303CD68D3BFD026D298BC884C84C05D |

These builds include 16 shared configuration checks in addition to the existing 109 Windows checks (125 distinct checks). Both applications run the shared configuration suite in self-test mode. Startup integration checks also launch the published CLI from a different working directory, verify environment precedence, and verify malformed-file errors without exposing values or contacting the server. The x86 verification uses WOW64; acceptance on a customer computer running actual 32-bit Windows remains pending.

Packages are generated locally under ignored `dist/`. Git tracks source and documentation, not delivery ZIPs.

## Build a delivery

From the repository on a Windows development machine with the .NET 9 SDK:

```powershell
./scripts/publish_windows.ps1
```

The default architecture is win-x64. Windows 32-bit requires the separate win-x86 package; the x64 executable cannot run on 32-bit Windows. Publish both applications with the included 32-bit runtime:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File ./scripts/publish_windows.ps1 -Runtime win-x86
```

To target Windows ARM64:

```powershell
./scripts/publish_windows.ps1 -Runtime win-arm64
```

If the machine disables unsigned PowerShell scripts, invoke this trusted repository script in a separate process without changing the machine's policy:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File ./scripts/publish_windows.ps1
```

The script publishes both projects with their runtime, single-file bundling, native-library self-extraction, no trimming, and no debug symbols. It stops on publishing failures and uses unique build staging and stable delivery names (`TytanUpdater-win-x86` or `TytanUpdater-win-x64`). If the destination directory or ZIP exists, move it elsewhere before publishing again. Build staging remains under ignored downloads/release-builds; distributable files and the delivery ZIP are under ignored dist. It prints the ZIP's SHA256 hash.

Each release contains:

| File | Use |
| --- | --- |
| Tytan.Updater.Desktop.exe | Double-click to open the main window |
| Tytan.Updater.Cli.exe | Run from a terminal with the two folder names |
| installation.example.json | Demonstration data for the desktop window only |
| .env.example | Optional server configuration template without a password |
| README.txt | End-user instructions, including credentials and console invocation |

Extract the whole ZIP before use. Runtime libraries bundled in each executable may be extracted automatically by .NET on launch. This is a portable delivery; it does not create shortcuts, register an installer, or install Tytan updates. An MSI/setup wizard is not needed for this delivery.

## User workflow

Double-click the desktop executable, load local installation information or the example, enter credentials, compare, and choose a newer ZIP to download. The desktop still uses its provisional installation JSON; its definitive local-file integration remains pending.

For the console, configure credentials as described in the included README, then run from PowerShell:

```powershell
.\Tytan.Updater.Cli.exe Barcin_Wodbar Faktury_008.000.042
```

Only a newer server package opens Save As to select the ZIP destination. An optional third parameter specifies an existing destination directory and bypasses Save As; no local JSON is required. The console needs an interactive Windows session for that dialog. Both executables use the existing hosted API and download endpoint; no server changes are required.

## Validate the published binaries

In the actual release directory, run:

```powershell
.\Tytan.Updater.Cli.exe --help
.\Tytan.Updater.Cli.exe --self-test
.\Tytan.Updater.Desktop.exe --self-test
```

The self-tests use local fixtures, without production requests. Desktop checks briefly open actual windows. Also open the desktop executable normally and perform acceptance testing on a representative customer computer. Architecture-specific packages must be tested on matching hardware. Building a self-contained package on a development computer does not by itself prove compatibility with every customer environment.

The current package checks and downloads ZIPs. Automatic Tytan installation remains outside the implemented behavior.

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

### Download without a dialog

The optional third argument is an existing local destination directory, not a ZIP filename:

```powershell
.\Tytan.Updater.Cli.exe Barcin_Wodbar Faktury_008.000.042 "C:\Tytan\Updates"
```

Create the directory first and ensure the account can write to it. Use quotes around paths containing spaces. Relative paths resolve against the terminal's current working directory. A newer package is saved directly as `<destination-folder>/<server-package-name>.zip`, without a customer subfolder or Save As dialog. An empty, missing, or file-valued destination is rejected before contacting the server. Existing ZIPs are preserved. If no newer version exists, the command prints `No updates available.` and downloads nothing.

Omit the third argument to keep the existing Save As workflow. Both modes download and validate ZIPs; neither installs them.

The current packages include `.env` support, the configurable URL, and the optional CLI destination parameter.
