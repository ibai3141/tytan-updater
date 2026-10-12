# Windows distribution

The user receives a ZIP containing two self-contained executables, a provisional example installation file, and English usage instructions. Source code, PHP files, credentials, the .NET SDK, and build directories are not included. No separate .NET runtime installation is required.

## October 12, 2026 delivery

The following newly published packages include the configurable URL and optional CLI destination directory:

| Architecture | ZIP | SHA256 |
| --- | --- | --- |
| 32-bit Windows | TytanUpdater-win-x86-20261012-091453-5e2e3c.zip | B75F132C060A3D344E8D73A54C5588B3D1A5EAA8697AF73523C20255D4DA64DD |
| 64-bit Intel/AMD Windows | TytanUpdater-win-x64-20261012-091529-a003b2.zip | 0483AB36AA4099232EE78A781E1FB57E90C38707AC53C6586845C96CA28C5A38 |

Packages are generated locally under ignored `dist/`; Git commits contain the source and documentation. Both published architectures passed 42 CLI checks and 67 desktop checks (109 total each), using local fixtures without production requests. The x86 executables were tested under WOW64 on the development computer; acceptance on an actual 32-bit customer Windows installation remains pending. The preserved October 9 packages were not replaced.

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

The script publishes both projects with their runtime, single-file bundling, native-library self-extraction, no trimming, and no debug symbols. It stops on publishing failures and creates uniquely named directories instead of deleting prior releases. Build staging remains under ignored downloads/release-builds; distributable files and the delivery ZIP are under ignored dist. It prints the ZIP's SHA256 hash.

Each release contains:

| File | Use |
| --- | --- |
| Tytan.Updater.Desktop.exe | Double-click to open the main window |
| Tytan.Updater.Cli.exe | Run from a terminal with the two folder names |
| installation.example.json | Demonstration data for the desktop window only |
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

The configurable URL and destination parameter require newly published executables. Preserved October 9 ZIPs are unchanged.
