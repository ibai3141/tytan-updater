# Implementation phases and delivery status

Updated: October 9, 2026. Commit messages are Spanish; documentation is English. The user handles pushing.

Current baseline: source restored in 9001bc9 to the first distribution, 45122ab / TytanUpdater-win-x64-20261009-094714-e05a10. The desktop loads a JSON/example; the console accepts two names and opens Save As. Later remembered-client/configuration UI changes were reverted. The retained desktop has 67 checks and the CLI has 24, for 91 Windows checks. See the [technical guide](technical-guide.md) and [user guide](user-guide.md) for the final retained behavior; phase counts below describe historical checkpoints.

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

The user also requires 32-bit support. A separate win-x86 package is now published with its x86 runtime. Both binaries were confirmed as 32-bit PE files and running WOW64 processes; all 91 Windows checks passed from the extracted x86 delivery on the development x64 computer. Acceptance on a real customer 32-bit Windows installation remains pending. The original x64 ZIP is preserved.
