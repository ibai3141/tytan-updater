# Windows distribution

The user receives a ZIP containing two self-contained executables, an administrator provisioning script, a provisional test/example installation file, and English usage instructions. Application source code, PHP files, credentials, the .NET SDK, and build directories are not included. No separate .NET runtime installation is required.

## Build a delivery

From the repository on a Windows development machine with the .NET 9 SDK:

```powershell
./scripts/publish_windows.ps1
```

The default architecture is win-x64. To target Windows ARM64:

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
| installation.example.json | Optional demonstration/test fixture; not required for normal use |
| README.txt | End-user instructions, including credentials and console invocation |
| configure_desktop.ps1 | Administrator setup of the customer's local configuration, once per Windows user |

Extract the whole ZIP before use. Runtime libraries bundled in each executable may be extracted automatically by .NET on launch. This is a portable delivery; it does not create shortcuts, register an installer, or install Tytan updates. An MSI/setup wizard is not needed for this delivery.

## User workflow

The administrator provisions the computer once with configure_desktop.ps1 (see the included README or [desktop configuration](desktop.md)). Existing valid folders.json settings from the previous version are reused. The user double-clicks the desktop executable, enters the password, and checks for updates; folder-name fields and example/import buttons are absent. The app loads only the configured customer. Missing/invalid settings show an administrator-setup message, without querying all clients. Choose a newer ZIP to download through Save As. Configured versions do not change when a ZIP is downloaded; the administrator updates configuration when the actual installation changes. Final automatic installed-version detection remains pending.

For the console, configure credentials as described in the included README, then run from PowerShell:

```powershell
.\Tytan.Updater.Cli.exe Barcin_Wodbar Faktury_008.000.042
```

Only a newer server package opens Save As to select the ZIP destination. No third parameter or local JSON is required. The console needs an interactive Windows session for that dialog. Both executables use the existing hosted API and download endpoint; no server changes are required.

## Validate the published binaries

In the actual release directory, run:

```powershell
.\Tytan.Updater.Cli.exe --help
.\Tytan.Updater.Cli.exe --self-test
.\Tytan.Updater.Desktop.exe --self-test
```

The self-tests use local fixtures, without production requests. Desktop checks briefly open actual windows. Also open the desktop executable normally and perform acceptance testing on a representative customer computer. Architecture-specific packages must be tested on matching hardware. Building a self-contained package on a development computer does not by itself prove compatibility with every customer environment.

The current package checks and downloads ZIPs. Automatic Tytan installation remains outside the implemented behavior; see the [installation contract](installation-contract.md).
