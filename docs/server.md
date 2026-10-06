# PHP server: listing and downloading update packages

Updated: October 6, 2026.

The corrected scope includes creating `api.php` and `download.php`. The repository now supplies both endpoints and `common.php`, their shared implementation. They use the contract in the original integration guide and work with the existing C# client. Tytan still handles installation.

## Files and deployment layout

Copy these three PHP files from `server/` into the hosting server's SQLupdate directory:

```text
SQLupdate/
    api.php
    download.php
    common.php
    .htaccess                  Existing hosting HTTPS/BasicAuth configuration
    Barcin_Wodbar/
        Faktury_008.000.043.zip
        FK2025_005.005.007.zip
    barczewo_zwik/
        ...
```

`server/.htaccess.example` is an Apache example, not a ready-to-upload configuration. Merge it with the existing hosting settings and replace its AuthUserFile placeholder. Do not overwrite the existing .htaccess. Create the password file outside the public document root using the hosting controls or htpasswd; never commit it.

Only the three PHP files are needed for the endpoints. Do not upload the whole repository, local tests, example .htaccess, or documentation to SQLupdate.

## Requirements and authentication

The endpoints are compatible with PHP 7.2 and PHP 8, tested on the hosting version PHP 7.2.34 and on PHP 8.5.11. They require JSON and filesystem access; no Composer dependencies, database, or ZIP extension is required. Files already exist as ZIPs; the server streams them without extracting them.

Production requests must use HTTPS. PHP checks the web server's HTTPS flag or port 443. With TLS termination upstream, configure the trusted hosting server to pass the correct HTTPS state. The endpoints do not trust client-supplied X-Forwarded-Proto headers.

There are two authentication modes:

1. **Hosting BasicAuth:** keep SQLupdate protected by the hosting's directory authentication. When neither PHP credential variable is configured, the endpoints require REMOTE_USER set by that trusted web server. This mode protects direct ZIP URLs as well as the PHP endpoints.
2. **PHP BasicAuth:** supply both TYTAN_API_USERNAME and TYTAN_API_PASSWORD through private server configuration. Each request must provide matching BasicAuth credentials. With this mode, either keep package directories protected by hosting authentication or set TYTAN_UPDATE_ROOT to a directory outside the public document root so direct ZIP URLs cannot bypass endpoint authentication.

Without either configured PHP credentials or a server-authenticated REMOTE_USER, the request fails with 503. Partially configured credentials also fail. Wrong or missing request credentials in PHP mode return 401 with a BasicAuth challenge.

Credentials are not hardcoded. Use the actual credentials from the original document in private hosting configuration; no credentials are included here.

| Setting | Purpose |
| --- | --- |
| TYTAN_API_USERNAME | Username when using PHP-managed BasicAuth |
| TYTAN_API_PASSWORD | Password when using PHP-managed BasicAuth |
| TYTAN_UPDATE_ROOT | Optional absolute package-directory path; defaults to the PHP files' directory |
| TYTAN_ALLOW_LOCAL_HTTP | Local test switch only; works solely on PHP's CLI server with a loopback request |

Do not enable the local HTTP switch in production. It does not relax HTTPS checks on Apache/FastCGI.

## api.php

```http
GET /SQLupdate/api.php
GET /SQLupdate/api.php?dir=Barcin_Wodbar
```

The root request lists folders and any ZIP packages at the root. The dir parameter selects a directory relative to the update root. The PHP endpoint also supports nested relative folders; the current C# client uses one-level client folders.

Response example, using illustrative values:

```json
[
  {
    "name": "Faktury_008.000.043.zip",
    "type": "file",
    "size": 17492992,
    "modified": "2026-09-29 09:15:00",
    "path": "Barcin_Wodbar/Faktury_008.000.043.zip"
  }
]
```

The response is an array, including [] for an empty directory. Fields match FileEntry in C#. Modified timestamps are UTC; the client uses numeric versions rather than dates to select packages.

PHP source files, other non-ZIP files, dotfiles, unreadable entries, and links resolving outside the update root are omitted. The server does not select a version or receive the client's installed version; that logic remains in the C# module.

## download.php

```http
GET /SQLupdate/download.php?file=Barcin_Wodbar/Faktury_008.000.043.zip
```

Use the relative path returned by the listing. The endpoint serves only readable ZIP files contained within the update root, with these headers:

```text
Content-Type: application/octet-stream
Content-Disposition: attachment; filename="..."; filename*=UTF-8''...
Content-Length: <actual open-file size>
Cache-Control: no-store
X-Content-Type-Options: nosniff
```

The file is streamed from an open handle rather than loaded entirely into memory. HEAD requests return headers without the body. PHP does not inspect package contents, compare versions, or install anything.

Publish packages as complete files: upload to a temporary non-ZIP name and rename when ready. Do not edit a published ZIP while clients are downloading it.

## Paths and errors

Both endpoints accept GET and HEAD. Query arrays, absolute paths, backslashes, traversal components, control characters, and hidden path segments are rejected. Canonical paths are checked against the update root with a separator boundary, preventing a sibling such as SQLupdate-other from matching SQLupdate.

| Status | Meaning |
| --- | --- |
| 200 | Listing or package returned |
| 400 | Invalid or missing required path parameter |
| 401 | Missing or incorrect credentials in PHP-managed authentication |
| 403 | HTTPS required |
| 404 | Missing path, path outside the root, wrong entry type, or non-ZIP download |
| 405 | Unsupported request method |
| 500 | Listing, metadata, or file operation failed |
| 503 | Authentication or package root not configured correctly |

Endpoint errors return JSON with an error field and an English message. Server-side exceptions log their type, message, source file, and line privately without exposing those details in the response. Direct access to common.php returns a plain 404 and no data.

The provided shared account can access all client folders under the configured root, as in the original documents. Client-folder filtering does not implement separate authorization for each customer.

## Local tests

With Python, PHP on PATH, and the .NET SDK installed:

```powershell
python tests/server/test_endpoints.py --with-client
```

If PHP is not on PATH:

```powershell
python tests/server/test_endpoints.py --php C:/path/to/php.exe --with-client
```

The harness creates temporary client folders and actual ZIPs, launches PHP's built-in server on loopback, checks authentication, listings, paths, byte-for-byte downloads, headers, HEAD, JSON encoding failures with private exception logs, and configuration failures, and stops the server afterward. A filesystem symlink escape case is skipped if the operating system does not allow creating symlinks; the sibling-prefix boundary check always runs.

With --with-client, it runs the 15 existing C# tests plus two integration cases against the actual PHP implementation. A test-only handler maps the C# requests to the local HTTP server. Production HTTPS checks and the production C# transport remain unchanged. This verifies the application contract and downloaded bytes, not production TLS or Apache configuration.

## Publishing and acceptance

The user has uploaded the three files. The initial authenticated API request returned HTTP 500, and the hosting runtime was confirmed as PHP 7.2.34 without str_starts_with. The shared implementation now uses strpos for prefix checks and explicitly checks json_encode failures instead of JSON_THROW_ON_ERROR.

Upload the updated common.php to SQLupdate, replacing the initial copy. api.php and download.php need no changes for this fix. No .htaccess change is required by this compatibility fix. Then:

1. Confirm PHP execution, HTTPS, root location, and hosting authentication.
2. Check that unauthenticated requests are rejected.
3. Call api.php?dir=Barcin_Wodbar with the actual credentials and inspect its JSON.
4. Download a known ZIP through download.php and verify its bytes and headers.
5. Run the existing C# CLI list and download commands against that URL.
6. Connect Tytan to the module and its existing installation flow.

Do not treat the local built-in-server tests as verification of the actual hosting configuration.
