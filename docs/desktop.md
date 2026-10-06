# Local updater application

## Agreed workflow

The local Windows application will identify the client from local information, query the corresponding folder on the existing cloud API, compare available package versions with locally installed versions, and allow downloading newer ZIPs. Installation of those ZIPs remains a separate responsibility to confirm with Tytan.

All clients will use the same existing BasicAuth account, as explicitly requested. Selecting dir from a local file determines which folder the application displays; it is not server-side customer authorization. Someone with that account can request another folder. The server remains unchanged for this workflow.

The definitive local file is being produced by another developer. Its path, structure, and update rules have not yet been supplied. The client identifier might instead come from a local folder name; this source is also pending. This phase uses an explicit JSON example and does not infer a client from arbitrary folders on the computer.

## Phase 1: local window and installation file

Implemented:

- Windows Forms application targeting the available .NET 9 Windows runtime.
- Open a selected local JSON file or use the clearly labeled example.
- Show the client folder, product names, and installed versions.
- Validate all input before changing the displayed installation.
- Show English errors and preserve the last valid data on a failed replacement.
- Keep JSON mapping in InstallationFileReader so the final format can be substituted.

No API call, credentials, remote version comparison, download, or installation is implemented in this phase. Available-version and status cells show Not checked. Loading the example does not detect actual installations on this computer and does not modify the local file.

## Provisional input contract

Source: examples/installation.example.json. These are sample values, not confirmed installed versions.

```json
{
  "clientFolder": "Barcin_Wodbar",
  "products": [
    { "name": "Faktury", "installedVersion": "008.000.042" },
    { "name": "FK2025", "installedVersion": "005.005.007" },
    { "name": "FK2026", "installedVersion": "005.005.039" }
  ]
}
```

Names must be valid single path/name segments. Installed versions must contain three groups of three digits. Products must be nonempty and unique, ignoring letter case. Unexpected JSON properties are rejected so a mismatched definitive format is reported rather than silently ignored. The format is intentionally provisional; agree it with the other developer before connecting their file.

## Run the window

Requires Windows and the .NET 9 SDK:

```powershell
cd C:\Users\Ibai\tytan-updater
dotnet run --project desktop/Tytan.Updater.Desktop --configuration Release
```

Click Load example to view the sample client and its three products. Open installation file selects your own compatible file. An optional positional argument loads a specified file on startup:

```powershell
dotnet run --project desktop/Tytan.Updater.Desktop --configuration Release -- examples/installation.example.json
```

The compiled executable is desktop/Tytan.Updater.Desktop/bin/Release/net9.0-windows/Tytan.Updater.Desktop.exe. It requires the .NET 9 Windows Desktop Runtime when run without the SDK. A standalone distribution is not part of this first phase.

## Code organization

| File | Responsibility |
| --- | --- |
| Program.cs | Initialize WinForms and optional startup file; dispatch self-test mode |
| MainForm.cs | Local file selection, example loading, product grid, errors |
| InstallationFileReader.cs | Parse and validate the provisional file into local installation models |
| DesktopChecks.cs | Check real file parsing and window state without network traffic |

## Verification

```powershell
dotnet build desktop/Tytan.Updater.Desktop --configuration Release
dotnet run --project desktop/Tytan.Updater.Desktop --configuration Release --no-build -- --self-test
```

The self-test briefly opens the actual window and checks the sample file, malformed JSON, folder traversal, malformed versions, duplicate/empty/null products, unexpected fields, rendered grid values, and preservation of valid data after an invalid replacement. It makes no network requests.

## Next phases

1. Connect the shared BasicAuth account to HTTPS API requests using the loaded clientFolder, not a root-client listing. Add timeout, cancellation, and HTTP/JSON errors. Do not embed the password in source or the installation example.
2. Select the newest package independently for each installed product and compare numeric versions. Display Update available, Up to date, or No package.
3. Download selected newer ZIPs to an explicit destination; verify completion, sizes, and ZIP readability. A download does not change the recorded installed version.
4. Adapt the reader to the definitive local file and confirm how Tytan applies packages and maintains that file after successful installation.

The first phase is deliberately limited so the interface and the local-file contract can be reviewed before adding cloud access.
