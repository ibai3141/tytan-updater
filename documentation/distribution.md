# Windows distribution

The user receives a ZIP containing two self-contained executables, a provisional example installation file, and English usage instructions. Source code, PHP files, credentials, the .NET SDK, and build directories are not included. No separate .NET runtime installation is required.

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

Only a newer server package opens Save As to select the ZIP destination. No third parameter or local JSON is required. The console needs an interactive Windows session for that dialog. Both executables use the existing hosted API and download endpoint; no server changes are required.

## Validate the published binaries

In the actual release directory, run:

```powershell
.\Tytan.Updater.Cli.exe --help
.\Tytan.Updater.Cli.exe --self-test
.\Tytan.Updater.Desktop.exe --self-test
```

The self-tests use local fixtures, without production requests. Desktop checks briefly open actual windows. Also open the desktop executable normally and perform acceptance testing on a representative customer computer. Architecture-specific packages must be tested on matching hardware. Building a self-contained package on a development computer does not by itself prove compatibility with every customer environment.

The current package checks and downloads ZIPs. Automatic Tytan installation remains outside the implemented behavior.
