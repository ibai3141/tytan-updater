TYTAN UPDATER FOR WINDOWS

Extract the ZIP into a writable folder before using either executable.
The package contains its .NET runtime. No SDK or separate .NET installation is required.
Use the build matching your Windows architecture: win-x86 for 32-bit Windows,
win-x64 for 64-bit Intel/AMD Windows, or win-arm64 for Windows ARM64.
The win-x86 package includes the 32-bit .NET runtime.

DESKTOP INTERFACE

Double-click Tytan.Updater.Desktop.exe.
Open your installation JSON file, or click Load example for a demonstration.
The included installation.example.json contains test versions, not detected installed versions.
Enter the server username and password supplied by your administrator.
Click Check for updates, select an available update, and click Download selected update.
Choose where to save the ZIP. Open download folder locates a completed download.
The final installed-version file integration is still pending; this interface uses the provisional JSON format.

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
