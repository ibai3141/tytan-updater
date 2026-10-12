# Tytan Updater - User guide

Tytan Updater checks whether a newer version is available and downloads its ZIP file. **Downloading does not install the update.** Follow Tytan's instructions to install the downloaded file.

## Before you start

Ask your administrator for:

- The program package for your computer: `win-x86` for 32-bit Windows or `win-x64` for 64-bit Windows. If unsure, ask which one to use.
- Your server username and password.
- Your installation information file, which tells the program your customer name and installed versions.

Right-click the program ZIP, choose **Extract All**, and open the extracted folder. Do not run the program from inside the ZIP.

## Check for updates in the window

1. Double-click **Tytan.Updater.Desktop.exe**.
2. Click **Open installation file** and select the file supplied by your administrator.
3. Check that the displayed customer and products are yours.
4. Enter your username and password if they are not already filled in.
5. Click **Check for updates**.

**Load example** is for demonstrations. It does not detect what is installed on your computer. Use your own installation file for real updates.

The results appear in **Installed products**:

| Message | What it means |
| --- | --- |
| Update available | A newer version is ready to download. |
| Up to date | You already have the latest available version. |
| Installed version is newer | The version supplied for your computer is newer than the server's version. |
| No package | No matching update was found. Ask your administrator to check. |

## Download an update

1. Click the product showing **Update available**.
2. Click **Download selected update**.
3. Choose where to save the file and click **Save**.
4. Wait for the download and file check to finish.
5. Click **Open download folder** to find the saved ZIP.

The progress bar may pause near 99% while the file is being checked. At 100%, the ZIP has been saved; the software has not been installed or updated yet.

Click **Cancel** to stop a download. If a file with the same name already exists, choose another folder or filename.

When you reopen the program, select your installation file again. The program does not remember which file you opened.

## Save connection settings once

Your administrator can prepare this for you. In the folder containing the program:

1. Copy **.env.example** and rename the copy to **.env**. Make sure it is not named `.env.txt`.
2. Open `.env` with Notepad.
3. Set the server address and username, then save:

```dotenv
TYTAN_API_BASE_URL=https://tytan.poznan.pl/SQLupdate/
TYTAN_API_USERNAME=TytanSQL
```

Close and reopen the program after changing the file. Keep the server address supplied by your administrator; it must end in `/`.

You can continue entering the password in the window. To save it for the console, your administrator can add:

```dotenv
TYTAN_API_PASSWORD=your-server-password
```

Replace the example with the real password. A password saved here is readable in the file, so keep `.env` private.

## Use the console, if needed

The window above is the easiest option. Use the console only if you have been asked to do so.

Open the extracted program folder in File Explorer. Click the address bar, type `powershell`, and press Enter. Make sure the server password has been configured by your administrator.

Run this example, replacing the customer and installed product/version with your own details:

```powershell
.\Tytan.Updater.Cli.exe Barcin_Wodbar Faktury_008.000.042
```

The first name is your customer folder on the server. The second is the installed product folder's name, including its version. Ask your administrator if you do not know these values.

If an update is available, a window asks where to save it. Otherwise, the console displays `No updates available.`

To save directly without opening that window, add an existing folder as the third value:

```powershell
.\Tytan.Updater.Cli.exe Barcin_Wodbar Faktury_008.000.042 "C:\Tytan\Updates"
```

Replace `C:\Tytan\Updates` with your chosen folder. Create it first if it does not exist. Keep quotation marks around the path. This extra value is only for the console.

## If something goes wrong

| Problem | What to do |
| --- | --- |
| Check for updates is unavailable | Open your installation file first. |
| Download selected update is unavailable | Select a product showing Update available. |
| Username or password rejected / HTTP 401 | Check your credentials with the administrator. |
| Password missing in the console | Ask the administrator to configure it in `.env` or the terminal. |
| Console opens and closes immediately | Start it from PowerShell using the example above. |
| Executable not recognized | Open PowerShell in the extracted program folder. |
| File already exists | Choose another filename or folder. |
| Download fails or takes too long | Check your connection and try again. |
| Download finishes but the software has not changed | The ZIP still needs to be installed following Tytan's instructions. |

If the problem continues, send your administrator the exact error message or a screenshot. Do not include your password.
