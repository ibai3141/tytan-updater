# tytan-updater

TytanSQL update distribution over HTTPS with BasicAuth: PHP endpoints list client folders and serve ZIP packages, while Windows desktop and command-line applications compare installed versions and download newer packages. Installation is handled separately by Tytan.

The repository contains the PHP server, both Windows applications, an example installation file, automated checks, publishing tools, and technical and user documentation in English.

## Desktop application

The desktop application opens a local installation JSON file and displays the client folder, products, and installed versions. Click Load example to try the provisional format, enter the shared account credentials, then click Check for updates. It queries that client's API directory and compares the newest valid ZIP version for each installed product. Installed products shows Update available, Up to date, Installed version is newer, or No package. The Cloud folder tab retains the full metadata listing. Select an Update available row and click Download selected update to save its ZIP. Downloads show progress, support cancellation, preserve existing files, and validate size and ZIP readability before completion. Installation is not performed.

```powershell
dotnet run --project desktop/Tytan.Updater.Desktop --configuration Release
```

See [Desktop workflow and run instructions](documentation/desktop.md). All clients will use the shared hosting account, as requested; selecting a client folder is not an authorization boundary. The definitive installation-file format remains pending from the other developer.

## Command-line application

The Windows console executable accepts two required folder names and an optional destination directory: the cloud client folder and the installed product folder, such as `Faktury_008.000.042`. It extracts the product/version without reading a JSON file. If a newer ZIP is found, it saves it in the optional destination directory, or opens Save As when that argument is omitted. Otherwise, it prints `No updates available.` without opening a dialog. Cancelling Save As stops the download. It does not install updates.

```powershell
dotnet run --project cli/Tytan.Updater.Cli --configuration Release -- Barcin_Wodbar Faktury_008.000.042
```

Set the server credentials in the process environment first. The configurable URL and destination parameter require a newly built executable; the preserved October 9 ZIPs retain their original behavior. See [CLI setup, arguments, credentials, output, and checks](documentation/cli.md).

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

## Delivered files

For customer computers, build a portable Windows ZIP with `./scripts/publish_windows.ps1`. It includes self-contained desktop and console executables, the desktop example file, and end-user instructions; customers do not need the source repository or a separate .NET installation. See [Windows distribution](documentation/distribution.md).

For 32-bit Windows, publish with `-Runtime win-x86`; this bundles x86 executables and their 32-bit runtime. The default win-x64 package is for 64-bit Intel/AMD Windows. Both architectures support the desktop and the CLI with two required arguments plus an optional destination directory.

| File | Purpose |
| --- | --- |
| server/api.php | JSON listing of folders and ZIP packages |
| server/download.php | Download an existing ZIP by relative path |
| server/common.php | Shared authentication, HTTPS, paths, and errors |
| server/.htaccess.example | Optional Apache configuration reference |
| tests/server/test_endpoints.py | Automated local endpoint checks |

Deploy the three PHP files to SQLupdate. Keep the hosting's working authentication and HTTPS configuration. The Apache example requires adaptation and must not overwrite the existing configuration blindly.

## Documentation

- [Documentation index](documentation/README.md)
- [Complete technical guide with source excerpts](documentation/technical-guide.md) — [Word](documentation/technical-guide.docx)
- [Desktop and console user guide](documentation/user-guide.md) — [Word](documentation/user-guide.docx)
- [Additional PHP technical walkthrough](documentation/technical-walkthrough.md) — [Word](documentation/technical-walkthrough.docx)
- [Server deployment and configuration](documentation/server.md)
- [URL usage and acceptance checks](documentation/uso.md)
- [Local Windows application](documentation/desktop.md)
- [Command-line updater](documentation/cli.md)
- [Windows executables and customer distribution](documentation/distribution.md)

## Production URLs

```text
https://tytan.poznan.pl/SQLupdate/api.php
https://tytan.poznan.pl/SQLupdate/api.php?dir=Barcin_Wodbar
https://tytan.poznan.pl/SQLupdate/download.php?file=Barcin_Wodbar/Faktury_008.000.043.zip
```

On October 6, 2026, the user supplied successful root and client JSON listings and confirmed that downloading also works. The hosting runtime is PHP 7.2.34. Local tests passed on PHP 7.2.34 and PHP 8.5.11. See the [technical guide](documentation/technical-guide.md) for validation results and integration limits.

## Local tests

The PHP endpoint tests require Python and PHP; no .NET SDK, Composer, or database is needed for those tests. The desktop build and checks separately require the .NET 9 SDK on Windows.

```powershell
python tests/server/test_endpoints.py --php C:/path/to/php.exe
```

The harness creates temporary ZIP fixtures and local PHP servers, validates responses and downloads, then stops the servers. It never contacts production.

## Regenerate the Word documents

```powershell
python -m pip install -r scripts/requirements-docs.txt
python scripts/export_technical_doc.py
```

Credentials are configured privately outside the repository. Only server PHP files belong on the hosting server.
