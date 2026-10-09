TYTAN UPDATER FOR WINDOWS

Extract the ZIP into a writable folder before using either executable.
The package contains its .NET runtime. No SDK or separate .NET installation is required.
Use the build matching your Windows architecture (win-x64 or win-arm64).

DESKTOP INTERFACE

Double-click Tytan.Updater.Desktop.exe.
First use: enter Client folder and Installed folder, for example:
Client folder: Barcin_Wodbar
Installed folder: Faktury_008.000.042
Use the customer's actual folder names and installed version.
Enter the server username and password supplied by your administrator.
Click Check for updates (or press Enter). After a successful check, the two folder names
are remembered for your Windows user. Next time, just enter the password and check.
Select an available update and click Download selected update.
Choose where to save the ZIP. Open download folder locates a completed download.
No example or installation JSON needs to be loaded. Optional JSON import remains available.
Only folder names are saved in %LOCALAPPDATA%\TytanUpdater\folders.json, never credentials.
When the actual installed version changes, edit Installed folder accordingly.
The included installation.example.json is demonstration/test data, not detected installed versions.

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
