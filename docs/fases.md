# Implementation phases and delivery status

Updated: October 9, 2026. Commit messages are Spanish; documentation is English. The user handles pushing.

| Phase | Work | Status |
| --- | --- | --- |
| 1. Initial client prototype | C# models, version comparison, HTTPS requests, downloads, CLI | Historical; removed from the current delivery at the user's request |
| 2. Corrected PHP scope | api.php, download.php, common.php, deployment guide, endpoint tests | Completed in 29983ec |
| 3. Hosting compatibility | Replace PHP 8 prefix function and post-7.2 JSON flag; improve private logging | Completed in 050ef7a; user subsequently confirmed working hosting |
| 4. Production acceptance | Root listing, client listing, package download | Listings provided by user; successful downloading reported by user |
| 5. PHP delivery documentation | Technical Markdown/Word, production evidence, remove C# dependencies | Included in the current delivery |
| 6. Tytan integration | Version comparison, download validation, installation in Tytan's application | Responsibility of Tytan; not verified in this repository |

The PHP-only tests run locally on temporary fixtures and do not modify production. Initial C# commits remain accessible in Git history. See verificacion.md for the historical test results and their limits.

## New local Windows updater: incremental phases

The latest agreed flow uses the same BasicAuth account for all clients and reads the requested client folder and installed versions from local information. The source and final format of that information are still being prepared by another developer. This is client-side folder selection, not server-side access isolation. The PHP server remains unchanged. Swagger is not part of this work.

| Phase | Deliverable | Status |
| --- | --- | --- |
| Desktop 1 | Local WinForms window, provisional JSON reader, example, input errors | Completed locally; build and 10 checks passed |
| Desktop 2 | HTTPS/BasicAuth query of the loaded client folder, cancellation and error handling | Completed; 17 simulated cloud checks and a real read-only production listing passed |
| Desktop 3 | Per-product numeric version comparison and clear availability status | Completed; 44 total checks and real example/server comparison passed |
| Desktop 4 | Select and download newer ZIPs with size/readability checks | Completed; 65 total checks and a real Faktury download passed |
| Desktop 5 | Adapt the final local file and confirm installation responsibilities with Tytan | Pending external contract |

Each desktop phase is delivered for review before moving to the next. Commits remain Spanish and the user handles pushing.

## Additional console delivery

On October 9, a separate Windows console executable was added at `cli/Tytan.Updater.Cli`. Following the user's final clarification, it accepts exactly two names: the cloud client folder and the installed product folder (for example, `Faktury_008.000.042`). When a newer ZIP is found, Save As lets the user choose its destination. No third argument is accepted and no dialog opens when up to date. It shares the desktop comparison/download implementation and does not install packages. Twenty-four revised CLI checks passed.

The CLI does not read an installation JSON or require a real local directory. The desktop JSON workflow remains separate. See [CLI instructions](cli.md) for credentials, arguments, output paths, exit codes, and distribution.

## Portable Windows delivery

The publishing script creates a Windows package containing self-contained desktop and console executables, the provisional desktop example, and end-user instructions. The first win-x64 ZIP was generated and extracted separately; all 24 CLI and 67 desktop checks passed using the published executables. No production requests were made. See [Windows distribution](distribution.md). Acceptance on representative customer hardware remains pending, as does the final desktop installation-file integration.

## Desktop direct entry and remembered folders

The desktop now accepts the same two folder names as the CLI directly in the window. After a successful check, it remembers only those names for the current Windows user; subsequent launches require only credentials and a check. No example or JSON import is needed. Optional file import remains available. Eleven new checks cover direct entry, settings persistence/restoration, no credential storage, stale-result clearing, invalid input, authentication failure, updated installed versions, and corrupt preferences. Together with existing checks, 78 desktop and 24 CLI checks pass. The Windows package is regenerated for this workflow.

## Latest desktop workflow: no folder inputs

The user's subsequent clarification supersedes the direct-entry UI above. The desktop now reads the already configured customer's settings automatically; the user only enters credentials and checks. Folder inputs and example/import buttons have been removed. An administrator provisions new Windows profiles once with the supplied configure_desktop.ps1. Existing settings remain compatible. Missing/invalid settings prevent requests and explain the setup requirement. Eleven revised configured-client checks replace the direct-entry checks; the desktop still has 78 checks in total.
