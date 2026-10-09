TYTAN UPDATER FOR WINDOWS

Extract the ZIP into a writable folder before using either executable.
The package contains its .NET runtime. No SDK or separate .NET installation is required.
Use the build matching your Windows architecture (win-x64 or win-arm64).

DESKTOP INTERFACE

Double-click Tytan.Updater.Desktop.exe.
The program loads the customer configured for this Windows user automatically.
Enter the server password and click Check for updates (or press Enter).
The username already defaults to TytanSQL.
Select an available update and click Download selected update.
Choose where to save the ZIP. Open download folder locates a completed download.
There are no folder-name fields or example/import buttons in the window.
If configuration is missing or invalid, contact the administrator.
The administrator maintains %LOCALAPPDATA%\TytanUpdater\folders.json, never credentials.
The included installation.example.json is demonstration/test data, not detected installed versions.

ADMINISTRATOR SETUP (ONCE PER WINDOWS USER)

Existing valid folders.json settings from the previous desktop version are reused.
For a new user, run the included setup script as that Windows user with the real
customer folder and installed product/version name. These values are examples:

powershell -NoProfile -ExecutionPolicy Bypass -File .\configure_desktop.ps1 -ClientFolder Barcin_Wodbar -InstalledFolder Faktury_008.000.042

Restart the desktop application after configuring it. Update this configuration
when the actual installed version changes; downloading does not change the version.
The setup script stores no credentials. It is an administrator provisioning step,
not an extra end-user step on each launch.

COMMAND LINE

Open PowerShell in the extracted folder.
Set credentials for that terminal session without showing the password:

$env:TYTAN_API_USERNAME = 'TytanSQL'
$secret = Read-Host 'Server password' -AsSecureString
$env:TYTAN_API_PASSWORD = [System.Net.NetworkCredential]::new('', $secret).Password

Run with exactly two parameters:

.\Tytan.Updater.Cli.exe Barcin_Wodbar Faktury_008.000.042

The first parameter is the client's cloud folder.
The second is the installed product folder's NAME, containing its version.
No JSON or existing local installation folder is required for the command line.
If a newer ZIP exists, Save As opens so you can choose the destination.
If no newer version exists, the command prints No updates available.
Cancel closes the operation without downloading. Existing files are preserved.

After use:

Remove-Item Env:TYTAN_API_PASSWORD
Remove-Variable secret

IMPORTANT BEHAVIOR

Both programs check versions and download ZIPs. Neither installs updates.
Downloading a ZIP does not change the installed version.
Both connect to https://tytan.poznan.pl/SQLupdate/ over HTTPS.
Credentials are supplied separately by the administrator, not included in this package.
All customers use the shared server account; client folder selection is not access isolation.
