# Tytan Updater — User guide

Updated: October 12, 2026.

This guide covers the current Windows application, with win-x64 and win-x86 (32-bit) builds. Use the current October 12 packages, which include optional `.env` configuration, the configurable API URL, and the optional console destination argument. It covers the desktop window and the console executable. The desktop has Open installation file and Load example buttons. It does not automatically load a customer's remembered configuration.

## What the program does

Tytan Updater connects to the company's HTTPS update server, checks available ZIP versions against the installed version information you supply, and lets you download a newer package to a location you choose.

It downloads and checks the ZIP. It does not install it. After downloading, follow the update-installation procedure provided by Tytan. A progress bar at 100% means the ZIP has been checked and saved; it does not mean the application has been updated.

There are two ways to use it:

| Program | How you start it | How you supply installed-version information |
| --- | --- | --- |
| Tytan.Updater.Desktop.exe | Double-click | Load an installation JSON file, or Load example for a demonstration |
| Tytan.Updater.Cli.exe | Run in PowerShell | Supply two required folder names and an optional download directory |

Both use the same server account supplied by the administrator. The password alone does not identify your customer folder or installed version.

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

## Before you begin

Choose win-x86 for 32-bit Windows or win-x64 for 64-bit Intel/AMD Windows. The win-x64 package requires 64-bit Windows; use the win-x86 package on a 32-bit computer. Each package includes its matching .NET runtime. You also need an internet connection to the update server, server credentials, and actual customer/product/version information supplied by your administrator. Choose a download folder you can write to.

You do not need Visual Studio, the .NET SDK, PHP, or the source repository. This package includes its .NET runtime. The console opens Save As when no destination argument is supplied; that mode requires an interactive Windows session. Supplying the destination parameter downloads without a dialog.

## Extract the package

1. Obtain the current Windows ZIP from the administrator.
2. Extract it into a folder on your computer before running either program.
3. Keep the executable files, example installation file, README, and `.env.example` together:

```text
Tytan.Updater.Desktop.exe
Tytan.Updater.Cli.exe
installation.example.json
.env.example
README.txt
```

The included JSON is sample data for demonstration. It does not describe the software installed on your computer. This package does not contain a configure_desktop.ps1 script and requires no such setup script.

## Use the desktop interface

### Open the program and load installed versions

1. Double-click Tytan.Updater.Desktop.exe.
2. Click Open installation file and choose the file prepared for your customer.
3. Confirm that the displayed client folder, product names, and installed versions are correct.

If you only want to try the interface, click Load example instead. The window labels the loaded versions as example data. Do not use sample values as proof of your actual installed version.

Check for updates is enabled after valid installation information has been loaded. If a replacement file is invalid, the previously loaded valid information remains displayed; read the file error before proceeding.

### Enter credentials and check

1. Username normally defaults to TytanSQL. Change it only if the administrator supplied a different account.
2. Type the server password in Password. Its characters are masked.
3. Click Check for updates.
4. Wait for the result. Use Cancel to stop a pending operation.

The program queries only the folder from the loaded installation information. The Installed products tab shows the comparison. The Cloud folder tab shows the entries returned for that folder, with package names, sizes, modification dates, and relative paths. The cloud tab is informational; it is not a customer-folder selection screen.

### Understand the results

| Status | Meaning | Next action |
| --- | --- | --- |
| Not checked | No successful comparison for this loaded information yet | Check for updates |
| Update available | The server has a higher version for that product | Select the row and download if required |
| Up to date | The supplied installed version equals the newest matching server version | No newer package to download |
| Installed version is newer | The supplied version is higher than the server package | No downgrade is offered; confirm the information if unexpected |
| No package | No valid matching product ZIP was found in that customer folder | Ask the administrator to check the folder and package names |

Versions are compared as numbers in three groups, not by the file modification date. A package for FK2025 is not treated as an update for FK2026 or Faktury.

### Download an update

1. In Installed products, select a row marked Update available.
2. Click Download selected update.
3. In Save As, choose your destination folder. The new ZIP filename is suggested automatically.
4. Click Save and wait for the transfer and ZIP check to complete.
5. Read Download complete and the full ZIP path in the status message.
6. Click Open download folder to locate the saved archive.

Download selected update is disabled if there is no eligible selected row, the Cloud folder tab is selected, or another operation is running. Switch back to Installed products and select the newer product row.

An existing destination file is not overwritten. Choose another filename or folder if the program reports that the destination already exists. Cancelling Save As returns without starting a transfer. Cancelling an active transfer removes its incomplete temporary file.

The bar stays below 100% until validation and final saving finish. The message Transfer complete. Checking the ZIP before saving the final file... means the network transfer has finished and archive checking is still running. A completed bar remains visible after success.

### Close and reopen

Closing the window cancels pending work. The program does not save your password or automatically remember the loaded installation file in this version. On the next launch, load the appropriate file again and enter the password.

An administrator can also launch the desktop with a known installation file path:

```powershell
.\Tytan.Updater.Desktop.exe "C:\Tytan\installation.json"
```

This supplies the file for that launch; it does not enable automatic version detection.

## Prepare a real desktop installation file

The administrator should supply this file. Its current format is JSON, for example:

```json
{
  "clientFolder": "Barcin_Wodbar",
  "products": [
    {
      "name": "Faktury",
      "installedVersion": "008.000.042"
    }
  ]
}
```

Replace the customer name and installed version with the real information. Save it as a .json file. The clientFolder must match the customer's folder on the server. Name is the product prefix, without the underscore, version, or .zip. installedVersion must have three groups of three digits, such as 008.000.042. Multiple product entries are allowed, with no repeated product names.

This is supplied metadata. The program does not inspect the computer to discover the installed version. Downloading an archive leaves the file unchanged. When Tytan actually installs a version, the file's owner must maintain the correct installed-version information.

## Use the console application

### Open PowerShell in the extracted folder

Open PowerShell and change to the folder where the package was extracted. For example:

```powershell
cd "$env:USERPROFILE\Downloads\TytanUpdater-win-x64"
```

Use your actual extracted-folder path. Running the CLI by double-clicking is not the intended workflow: without parameters it prints usage and closes immediately. Running it inside PowerShell keeps its messages visible.

### Set the credentials for this terminal session

```powershell
$env:TYTAN_API_USERNAME = 'TytanSQL'
$secret = Read-Host 'Server password' -AsSecureString
$env:TYTAN_API_PASSWORD = [System.Net.NetworkCredential]::new('', $secret).Password
```

Enter the password when prompted; its literal value is not displayed or written into the command. These variables are used by the program launched from this terminal. They are not automatically carried into a different terminal session. The application does not write them to a credentials file.

### Download without a dialog

The optional third argument is an existing local destination directory, not a ZIP filename:

```powershell
.\Tytan.Updater.Cli.exe Barcin_Wodbar Faktury_008.000.042 "C:\Tytan\Updates"
```

Create the directory first and ensure the account can write to it. Use quotes around paths containing spaces. Relative paths resolve against the terminal's current working directory. A newer package is saved directly as `<destination-folder>/<server-package-name>.zip`, without a customer subfolder or Save As dialog. An empty, missing, or file-valued destination is rejected before contacting the server. Existing ZIPs are preserved. If no newer version exists, the command prints `No updates available.` and downloads nothing.

Omit the third argument to keep the existing Save As workflow. Both modes download and validate ZIPs; neither installs them.

### Run with the two names

```powershell
.\Tytan.Updater.Cli.exe Barcin_Wodbar Faktury_008.000.042
```

- First parameter: the customer's cloud folder name, here Barcin_Wodbar.
- Second parameter: the installed product folder name, here Faktury_008.000.042.

Replace both examples with the correct values. The second parameter is just a name, not C:\... or another full path. Do not add .zip. No JSON file or existing local folder with that name is needed. The program obtains Faktury and 008.000.042 from the name you supply. A third destination parameter is not supported.

If a newer server package is available, Save As opens with the new ZIP filename. Choose the destination there. The console then shows download progress, ZIP validation, and ZIP saved to followed by the final path.

If the server version is equal or older, the terminal prints:

```text
No updates available.
```

If no matching product package exists, it reports an error instead. This is different from a successful up-to-date comparison. If you cancel Save As, no ZIP is requested. Ctrl+C cancels an active operation.

For help:

```powershell
.\Tytan.Updater.Cli.exe --help
```

To view the exit code immediately after running the CLI:

```powershell
$LASTEXITCODE
```

| Code | Meaning |
| --- | --- |
| 0 | Successful comparison/download, including no newer update |
| 1 | Error: read the accompanying message |
| 2 | Incorrect command arguments |
| 130 | Cancelled by the user |

Remove the password from the current terminal environment when finished:

```powershell
Remove-Item Env:TYTAN_API_PASSWORD
Remove-Variable secret
```

## Worked example

Suppose the actual supplied installed version is Faktury 008.000.042 and the server folder contains Faktury_008.000.043.zip. The comparison finds a newer package. Save As suggests Faktury_008.000.043.zip; you choose your Downloads folder and let it finish.

The saved ZIP can now be passed to Tytan's installation procedure. The program has not installed 008.000.043. Repeating the comparison with supplied version 008.000.042 still finds that update. If you choose the same already-existing output file, the program refuses to overwrite it.

If the actual supplied installed version is already 008.000.043 and the server's newest matching version is 008.000.043, there is no newer package. The desktop shows Up to date; the console prints No updates available. and does not open Save As.

## Troubleshooting

| Symptom or message | What to do |
| --- | --- |
| CLI opens and closes immediately | Run it from PowerShell with the two required names |
| Set TYTAN_API_PASSWORD before running the command. | Set the credentials in the same PowerShell session, then run again |
| Check for updates is disabled in the desktop | Load a valid installation JSON or the clearly labeled demonstration example |
| Download selected update is disabled | Select an Update available row in Installed products and wait for any running operation |
| HTTP 401 / credentials rejected | Check the shared username/password with the administrator |
| HTTP 403 | Confirm that you are using the normal HTTPS server and ask the administrator to check access |
| HTTP 404 / folder not found | Verify the exact cloud customer folder and product/package information |
| HTTP 500 / server could not complete the request | Ask hosting support/administrator to inspect the private PHP error log |
| HTTP 503 | Ask the administrator to check server authentication/root configuration or availability |
| No package | Check that the product and customer are correct and a matching ZIP exists on the server |
| Invalid installation JSON | Check exact field names, version format, commas, product names, and duplicate entries |
| Destination already exists | Select a different ZIP filename or destination folder |
| Package size changed / incomplete download / unreadable ZIP | Check for updates again and retry; ask the administrator if it repeats |
| Progress pauses near 99% | Read the message: ZIP validation may still be running |
| Progress is 100% but software is unchanged | The ZIP is saved; installation is a separate Tytan step |
| Connection failure or timeout | Check network access to the update server and retry |

When reporting an issue, include the program used, customer/product/version names, time, exact error text, and whether Save As opened or a ZIP was saved. Do not include the password. A screenshot can help if it shows the status message without exposing credentials.
