# Verification record

## October 5, 2026: live server query

A GET request with BasicAuth was sent to:

```text
https://tytan.poznan.pl/SQLupdate/api.php?dir=Barcin_Wodbar
```

Result: **HTTP 404**. No packages were downloaded. The request did not follow redirects, and credentials were read from the original document without storing them in the repository. A subsequent query using the implemented CLI also returned HTTP 404.

The user confirms the endpoints are published; this result does not confirm the actual contract at that address. The exact published path or access must be checked with the server administrators. A 404 alone does not identify the cause.

Implementation and local testing use the guide's contract. They do not validate the live server or Tytan's application.

## Local implementation validation

- Release build with .NET SDK 9.0.304: zero errors and zero warnings.
- Test executable: 15 of 15 cases passed. Coverage includes comparison, JSON, HTTP errors, complete downloads, invalid ZIPs, size mismatches, interruptions, cancellation, cross-client paths, destination preservation, and concurrency.
- CLI: a complete demo returned `Downloaded`; repeating it returned `Error` and exit code 1 while preserving the existing ZIP; help returned exit code 0.
- The demo ran in a temporary folder, and its sample package was removed afterward.
- Whitespace and patch formatting were checked using `git diff --check`.
- After translating console messages into English, all 15 tests passed again, and the demo printed `Package downloaded; Tytan can proceed with installation.`

Tytan's actual application is not in the repository: an integration example has been delivered, rather than integration executed inside its program. No live download has been tested because the documented listing endpoint returned 404.
