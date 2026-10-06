# Checking and downloading TytanSQL updates

Updated: October 6, 2026.

## 1. Objective and scope

Implement the PHP server endpoints api.php and download.php, plus the C# module that TytanSQL calls to query a client folder, detect a newer product version, and download its ZIP to a local folder. Hosting deployment is pending.

Tytan applies the package and manages the installed version. Our module finishes by returning the complete file and its result. It does not inspect the installation, extract packages, replace executables, execute SQL, or implement installation backups or recovery.

There is enough information to start: the language, authentication, endpoints, JSON listing, and comparison rule are documented. An input/output interface allows development and testing without requiring Tytan's complete source code.

## 2. Sources and confirmations

| Source | Information |
| --- | --- |
| `SQl_Update_Projekt_v_1.0.docx` | One folder per client, version stored in the application, and downloading a newer version |
| `SQl_Update_Projekt_v_1.1.docx` | The folder contains ZIPs of the latest versions of all the client's products |
| `Connect to your SQLupdate directory on the Idea server.docx` | C#, HttpClient, BasicAuth, JSON listing, and downloading; client and server examples |
| `Kopia zapasowa ... .wbk` | A shorter backup of the guide without additional requirements |
| User, October 6, 2026 | Correction: this project must create both PHP endpoints; use the available environment; Tytan handles installation |

The originals are in `F:\SQL_Update`. The `.~lock....docx#` file is temporary. Credentials are not copied into the repository.

The earlier assumption that endpoints were published was corrected on October 6. PHP and C# implementations are now tested together locally; production deployment remains pending. The October 5 HTTP 404 is recorded in [Verification record](verificacion.md). See [PHP server](server.md) for deployment and [Usage](uso.md) for the client. JSON values below are illustrative.

## 3. Responsibilities

| TytanSQL | This repository's module |
| --- | --- |
| Supply the client folder, product, and installed version | Query the folder and compare product versions |
| Supply access configuration and a local destination | Authenticate, download, and save a complete package |
| Decide when to check for updates | Report no update, download completion, cancellation, or error |
| Install the package and record the installed version | Return the local path and downloaded package version |

## 4. PHP API to deploy

Base URL: `https://tytan.poznan.pl/SQLupdate/`.

Every request requires HTTPS and BasicAuth. Configure HttpClient using credentials supplied through external configuration. Do not embed credentials in code, URLs, examples, or logs.

### Listing

```http
GET /SQLupdate/api.php
GET /SQLupdate/api.php?dir=Barcin_Wodbar
```

The first request explores folders. If Tytan already provides the client folder, query the second directly.

| JSON field | Content |
| --- | --- |
| `name` | File or folder name |
| `type` | `file` or `folder` |
| `size` | Size in bytes; can be null for folders |
| `modified` | Modification date as text |
| `path` | Relative path for downloading |

Illustrative example based on the guide's schema:

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

Explicitly map lowercase JSON fields to C# properties, or configure case-insensitive deserialization. The guide shows properties such as `Name`, while its PHP returns `name`. The implementation uses explicit JSON property mappings.

### Downloading

```http
GET /SQLupdate/download.php?file=Barcin_Wodbar/Faktury_008.000.043.zip
```

Use the selected file's `path` field and encode query parameter values. Keep requests within the configured base URL.

The implemented download.php returns binary content with an application/octet-stream content type, a download filename, and an actual file size. api.php returns folders and ZIP packages while excluding dotfiles and non-package files. common.php supplies authentication, HTTPS, path validation, and JSON errors. The three files must be placed in SQLupdate; see [PHP server](server.md).

## 5. Proposed contract with Tytan

The source documents do not define method signatures. The implemented interface follows this proposed contract.

Inputs:

- Base URL and credentials from external configuration.
- Client folder, such as `Barcin_Wodbar`.
- Product or fixed prefix, such as `Faktury`.
- Installed version, such as `008.000.042`.
- Local destination folder.
- A cancellation token.

The version is an input value. The module does not search the registry, databases, or installation files for it.

| Result | Returned information |
| --- | --- |
| No update | Product, installed version, and available version if one exists |
| Downloaded | Product, available version, and local path of the complete ZIP |
| Error | A useful reason for Tytan; no path presented as a valid download |
| Cancelled | Operation interrupted without returning a partial package |

If no package exists for the product, explicitly report that no package is available. Access failures or invalid JSON are errors, not an absence of updates.

The module reports a downloaded version, not an installed one. Tytan records the version after applying the ZIP.

## 6. Version selection and comparison

The documents show these names, with the ZIP extension hidden in the screenshots:

```text
Faktury_008.000.043.zip
FK2025_005.005.007.zip
FK2026_005.005.040.zip
```

Initial rule: `<product>_<version>.zip`, with three numeric components separated by periods.

1. Keep entries of type `file` that are ZIP packages.
2. Filter by the requested product, including the `_` separator to avoid matching other products.
3. Extract and validate the version.
4. Compare its components numerically.
5. Select the highest version of the same product.
6. Download only if it is newer than the installed version.

```text
008.000.043 -> (8, 0, 43)
008.002.066 -> (8, 2, 66)
(8, 2, 66) > (8, 0, 43)
```

Equal or lower versions do not trigger downloads. Products are compared separately. Ignore filenames that do not match the pattern; an invalid input version is an error.

`modified` is informational. Although one guide example sorts by date, the requirement is to compare versions: copying a package recently does not make its version newer.

The requirement is to obtain the latest package. Installation rules and any need for intermediate steps belong to Tytan.

## 7. Workflow

```text
Receive client folder, product, version, and destination from Tytan
    |
Validate inputs and prepare HttpClient with BasicAuth
    |
Query api.php?dir=<folder>
    |
Deserialize and select the product's highest-version ZIP
    |
Compare with the installed version
    +-- No package or equal/lower version -> return no update
    +-- Newer version
         |
       Download with download.php?file=<path> to a temporary file
         |
       Check download completeness and ZIP readability
         |
       Publish the file in the local destination
         |
       Return path and version to Tytan
```

Stream downloads rather than loading the entire package into memory. A partial file must not occupy the final filename or be returned as ready. Check size when available and read the ZIP without extracting it. The documented API offers no hash, so do not assume cryptographic package verification.

## 8. Module structure

| C# component | Responsibility |
| --- | --- |
| Configuration | Server, authentication, and connection options |
| API client | HTTPS, JSON reading, and downloading |
| Remote entry model | `name`, `type`, `size`, `modified`, and `path` fields |
| Version comparer | Numeric interpretation and selection by product |
| Update service | Coordinate queries, comparison, and downloading |
| Input and result models | Contract for calls from Tytan |

Use the available .NET environment and adjust compatibility during integration. The CLI can exercise the module without the complete application; the deliverable is the module callable from Tytan.

## 9. Errors and file handling

- Distinguish rejected authentication, missing folders or files, server errors, invalid JSON, and connection failures.
- Handle timeouts and cancellation. Any future retries must be limited; the current implementation does not retry automatically.
- Validate names and paths: reject absolute paths and `..` components; keep downloads within the requested client's folder.
- Keep local files within the configured destination and detect insufficient permissions or space.
- Avoid simultaneous writes to the same final file. Existing ZIPs are preserved and reported as errors.
- Clean up temporary files from failed operations without deleting complete packages belonging to other operations.
- Report products, versions, and results without exposing credentials.

## 10. Phases and acceptance criteria

| Phase | Work | Acceptance criterion |
| --- | --- | --- |
| 1. Contract and comparison | Models, filtering, and versions using sample listings | Correctly distinguish products and newer, equal, and older versions |
| 2. HTTPS queries | BasicAuth, JSON, and errors | Interpret the documented schema; a live test must confirm the actual contract |
| 3. Download and delivery | Temporary file, validation, and local path | Return a complete ZIP or a clear failure; never present a partial file as ready |
| 4. Integration | Connect the call and output path to Tytan | Tytan receives the result and continues its existing workflow |

The client, CLI, and PHP server implementation are delivered and tested together locally. Hosting deployment, production verification, and connection to Tytan's actual application remain pending.

## 11. Required verification

- Numeric comparison, including changes to any of the three components.
- Multiple products and multiple versions of one product.
- Lowercase JSON field mapping and permitted null values.
- Empty lists, invalid filenames, and invalid input versions.
- Rejected authentication, HTTP errors, and responses that are not JSON.
- Parameters containing spaces or characters requiring encoding.
- Invalid paths or paths belonging to another client.
- Successful, cancelled, and interrupted downloads, and invalid ZIPs.
- Existing destinations, insufficient permissions, and concurrent writes.

Rules can be tested locally with sample responses and ZIPs. Live testing verifies the server contract; it does not require load testing or Tytan installation testing.

## 12. Remaining integration details

- The exact calling interface and compatibility with Tytan's .NET project.
- How credentials and the local destination are provided in the application.
- Hosting configuration and deployment of the PHP endpoints, followed by production response and package verification.
- How Tytan handles existing ZIPs and consumes diagnostic results.

These details do not prevent local development. The implemented PHP/C# contract has been checked locally; verify it again after hosting deployment and Tytan integration.
