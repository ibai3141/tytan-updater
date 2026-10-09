# Installation integration contract

Updated: October 7, 2026.

The desktop application already reads provisional local data, queries the customer's cloud folder, compares package versions, and downloads validated ZIPs. This document records the information needed to adapt it to the real installation source and connect Tytan's installation mechanism. It does not define an installer or claim automatic installation is implemented.

The current desktop loads administrator-provisioned per-user folder settings automatically, with no folder-name inputs in the window. This identifies the customer and supplies a recorded product/version while the definitive installation-file integration remains pending. See [desktop configuration](desktop.md). The shared password itself does not identify a customer or installed version.

Review of F:\SQL_Update found only the original project/integration documents. They explain HTTPS listing and local ZIP saving but do not supply the definitive installed-version file or installation command. The provisional examples/installation.example.json remains example data.

## Information to provide

| Item | Required detail | Current state |
| --- | --- | --- |
| Real installation file | A representative file, its encoding/format, and its normal path on a customer's PC | Pending from the developer producing it |
| Client folder source | Which field or local folder identifies the matching cloud client directory | Not yet confirmed |
| Product/version mapping | How product names and installed versions are represented, including missing/uninstalled products | Provisional JSON only |
| File owner | Which application writes the installed-version file and when it refreshes it | Pending |
| Installation entry point | Existing Tytan command, executable, function, or manual procedure for consuming a downloaded ZIP | Not supplied |
| Installation inputs | Required package path, product identifier, target installation path, and arguments | Not supplied |
| Completion signal | How to distinguish success, failure, and a still-running installer | Not supplied |
| Runtime behavior | Whether Tytan must be closed and whether an external installer must continue after this window closes | Not supplied |

A command is needed only if Tytan uses an external installer. If Tytan handles installation through its own code, its owner should provide that integration contract instead. Do not invent a command by choosing an executable found inside a ZIP.

## Existing provisional boundary

InstallationFileReader maps a file into LocalInstallation (clientFolder plus product names and installed versions). The window, comparison, and download code consume that model. Once a real sample is supplied, adapt this reader and its fixtures while keeping the agreed client/product semantics. Do not infer an installation path or versions from arbitrary folders.

ProductUpdate retains the selected package and its version. DownloadAsync returns the verified saved ZIP path. That path can become the input to Tytan's confirmed installer once the entry point is known. No archive is currently extracted or executed.

## Intended completion flow

1. Read the definitive local information and identify the client/product.
2. Query and compare available versions, as implemented.
3. Download the selected newer ZIP and finish its checks, as implemented.
4. Invoke the confirmed installation mechanism with its documented inputs.
5. Wait for the documented completion signal; report failure without changing the recorded installed version.
6. On success, have the confirmed file owner refresh the installed-version source, then read it again to verify the actual installed version.

The updater should write that source only if its ownership and format explicitly assign this responsibility to it. A downloaded ZIP, a successfully launched process, or a closed installer window alone does not establish installation success.

## Acceptance with Tytan

- The real file identifies the correct cloud folder and each installed product/version.
- Missing or invalid local information prevents selecting the wrong installation.
- The confirmed installer receives the verified ZIP and correct product/destination inputs.
- Failed or cancelled installation leaves the recorded version unchanged.
- Successful installation refreshes the real local information and is verified by reading it again.
- Automatic installation is claimed only after these checks pass in the actual Tytan environment.

Phase 5 is pending these inputs. The existing application remains usable for checking versions and downloading packages, and no server-side change is required by this preparation.
