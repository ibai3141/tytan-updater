# Verification record

## October 5, 2026: live server query

A GET request with BasicAuth was sent to:

```text
https://tytan.poznan.pl/SQLupdate/api.php?dir=Barcin_Wodbar
```

Result: **HTTP 404**. No packages were downloaded. The request did not follow redirects, and credentials were read from the original document without storing them in the repository. A subsequent query using the implemented CLI also returned HTTP 404.

At that time, the working assumption was that endpoints were already published. On October 6, the user corrected this: the project must create them. The historical 404 does not validate the new implementation or identify its original cause.

The client was initially tested against the guide's contract. The new PHP endpoints are now exercised locally as recorded below; production hosting and Tytan's application remain unverified.

## Local implementation validation

- Release build with .NET SDK 9.0.304: zero errors and zero warnings.
- Test executable: 15 of 15 cases passed. Coverage includes comparison, JSON, HTTP errors, complete downloads, invalid ZIPs, size mismatches, interruptions, cancellation, cross-client paths, destination preservation, and concurrency.
- CLI: a complete demo returned `Downloaded`; repeating it returned `Error` and exit code 1 while preserving the existing ZIP; help returned exit code 0.
- The demo ran in a temporary folder, and its sample package was removed afterward.
- Whitespace and patch formatting were checked using `git diff --check`.
- After translating console messages into English, all 15 tests passed again, and the demo printed `Package downloaded; Tytan can proceed with installation.`

Tytan's actual application is not in the repository: an integration example has been delivered, rather than integration executed inside its program. No live download has been tested because the documented listing endpoint returned 404.

## October 6, 2026: actual PHP and C# integration

- Scope corrected: this project creates api.php and download.php; they were not an existing dependency to assume published.
- Portable PHP 8.5.11 was downloaded from the official PHP distribution and its SHA-256 was verified. It was used only in a temporary directory for local checks.
- PHP syntax checks passed for api.php, download.php, and common.php.
- 36 actual-PHP checks passed, including authentication, listings, empty directories, malformed parameters, traversal, sibling-prefix containment, downloads, byte equality, headers, HEAD requests, HTTP rejection, missing credentials, and invalid package-root configuration.
- The filesystem symlink escape test was skipped because the Windows environment did not allow creating symlinks. The direct path-containment boundary check passed.
- 17 C# tests passed: the existing 15 plus actual-PHP listing and package download/no-update integration.
- The .NET Release build completed with zero errors and zero warnings.
- Local PHP used its built-in server on loopback HTTP. A test-only handler connected the C# client to it. Production HTTPS transport, Apache BasicAuth, and hosting publication are not validated by these checks.
- No production files were uploaded or changed. Deployment remains pending; see [Server setup](server.md).
