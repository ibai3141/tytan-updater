# Tytan Updater - PHP server technical documentation

Updated: October 6, 2026.

This document describes the delivered PHP server, its source code, deployment, integration contract, and observed results. The previous C# library/CLI prototype was removed at the user's request. A later clarification introduces a local Windows application, developed incrementally under desktop/. Its phase 1 window and provisional local installation file are documented separately in desktop.md. This guide remains the PHP server reference.

## 1. Purpose and responsibilities

The server publishes client-specific ZIP packages over HTTPS with BasicAuth. api.php lists available entries, download.php streams a chosen package, and common.php supplies shared request checks. Tytan knows the installed version and chooses and installs the update.

The endpoints read existing folders and ZIPs. They do not require a database, Composer, or the PHP ZIP extension. They do not modify the package store. A .NET runtime is not required.

## 2. Repository and hosting layout

```text
tytan-updater/
    server/api.php
    server/download.php
    server/common.php
    server/.htaccess.example
    tests/server/test_endpoints.py
    scripts/export_technical_doc.py
    scripts/requirements-docs.txt
    docs/
```

Copy only the three PHP files to the hosting SQLupdate directory alongside the existing client folders. common.php is included by both endpoints; direct HTTP access returns 404. The Apache example is a configuration reference and must be adapted to the hosting setup.

```text
SQLupdate/
    api.php
    download.php
    common.php
    .htaccess
    Barcin_Wodbar/
        FK2025_005.005.007.zip
        FK2026_005.005.040.zip
        Faktury_008.000.043.zip
    barczewo_zwik/
        ...
```

## 3. Request flow

1. The application sends a GET or HEAD request over HTTPS with authentication.
2. The endpoint includes common.php and calls bootstrap().
3. bootstrap registers private exception logging, checks method and HTTPS, authenticates, and resolves the update root.
4. query_path validates the relative query parameter; resolve_target canonicalizes the requested path and checks containment.
5. api.php reads entry metadata and returns JSON, or download.php opens a ZIP and streams it.
6. Tytan interprets the response and continues its own version selection and installation flow.

The same checks run for HEAD requests, which return headers without a response body.

## 4. api.php: directory listing

The optional dir parameter selects a relative directory. Omitting it lists the root. The code rejects unreadable or non-directory targets, then scans the immediate directory. Hidden names, PHP/configuration files, non-ZIP files, unreadable entries, and links resolving outside the update root are omitted. Nested folders are supported through slash-separated relative paths.

Each result has name, type, size, modified, and path. Files have a byte size; folders have null. gmdate produces UTC timestamps. An empty result remains an array ([]). Directory scan order is not version selection.

Source: `server/api.php`

```php
<?php
declare(strict_types=1);

define('TYTAN_ENDPOINT', true);
require __DIR__ . '/common.php';

$base = bootstrap();
$relative = query_path('dir', true);
$target = resolve_target($base, $relative);

if (!is_dir($target) || !is_readable($target)) {
    fail_request(404, 'Directory not found or not readable.');
}

$items = scandir($target);
if ($items === false) {
    fail_request(500, 'The directory could not be listed.');
}

$result = [];
foreach ($items as $name) {
    // Publish folders and ZIP packages, never PHP source, configuration or dotfiles.
    if (!public_name($name)) {
        continue;
    }

    $path = realpath($target . DIRECTORY_SEPARATOR . $name);
    if ($path === false || !inside_root($path, $base) || !is_readable($path)) {
        continue;
    }

    $folder = is_dir($path);
    if (!$folder && (!is_file($path) || strtolower(pathinfo($name, PATHINFO_EXTENSION)) !== 'zip')) {
        continue;
    }

    $size = $folder ? null : filesize($path);
    $modified = filemtime($path);
    if ($size === false || $modified === false) {
        fail_request(500, 'Package metadata could not be read.');
    }

    // Return the lowercase JSON fields defined by the integration contract.
    $result[] = [
        'name' => $name,
        'type' => $folder ? 'folder' : 'file',
        'size' => $size,
        'modified' => gmdate('Y-m-d H:i:s', $modified),
        'path' => ($relative === '' ? '' : $relative . '/') . $name,
    ];
}

send_json($result);
```

## 5. download.php: ZIP streaming

The required file parameter is a relative path taken from the listing. Both the requested name and canonical target must have a ZIP extension. The target must be a readable regular file within the update root.

The script opens the ZIP in binary read mode. fstat reads its size from the open handle, and Content-Length describes that file. Content-Disposition provides an ASCII fallback filename and a UTF-8 encoded filename. application/octet-stream requests a binary download; no-store and nosniff are also emitted.

PHP buffering and compression are disabled before fpassthru streams the handle. The full package is not read into a PHP string. HEAD skips streaming. The finally block closes the handle during normal flow and exception unwinding; endpoint error helpers terminate the request and PHP releases request resources.

The endpoint does not extract or inspect ZIP contents and does not implement HTTP Range/resume support. Avoid changing a ZIP during download: upload a temporary non-ZIP filename and rename it after completion. After binary output has started, a failed transfer cannot be replaced with a clean JSON error; the application must validate completion.

Source: `server/download.php`

```php
<?php
declare(strict_types=1);

define('TYTAN_ENDPOINT', true);
require __DIR__ . '/common.php';

$base = bootstrap();
$relative = query_path('file', false);
$target = resolve_target($base, $relative);

// The download endpoint serves update packages only, not arbitrary server files.
if (!is_file($target) || !is_readable($target) ||
    strtolower(pathinfo($relative, PATHINFO_EXTENSION)) !== 'zip' ||
    strtolower(pathinfo($target, PATHINFO_EXTENSION)) !== 'zip') {
    fail_request(404, 'ZIP package not found.');
}

$stream = fopen($target, 'rb');
if ($stream === false) {
    fail_request(500, 'The package could not be opened.');
}

try {
    // Read size from the open handle so Content-Length describes the streamed file.
    $stat = fstat($stream);
    if ($stat === false) {
        fail_request(500, 'Package metadata could not be read.');
    }

    $name = basename($relative);
    $fallback = preg_replace('/[^A-Za-z0-9._-]/', '_', $name);
    header('Content-Type: application/octet-stream');
    header('Content-Disposition: attachment; filename="' . $fallback . '"; filename*=UTF-8\'\'' . rawurlencode($name));
    header('Content-Length: ' . $stat['size']);
    header('Cache-Control: no-store');
    header('X-Content-Type-Options: nosniff');

    // Disable PHP output buffering/compression to preserve byte counts and stream.
    ini_set('zlib.output_compression', '0');
    while (ob_get_level() > 0) {
        if (!ob_end_clean()) {
            break;
        }
    }

    if (($_SERVER['REQUEST_METHOD'] ?? '') !== 'HEAD') {
        fpassthru($stream);
    }
} finally {
    fclose($stream);
}
```

## 6. common.php: initialization and authentication

bootstrap hides displayed PHP errors and registers an exception handler. The handler records exception type, message, source file, and line in the private server log. Before headers have been sent, the response is a generic JSON error. PHP parse/startup errors and hosting rejections can occur before this handler and require the hosting log.

Only GET and HEAD are allowed. HTTPS is detected from the trusted web server's HTTPS flag or port 443. The only HTTP exception is an explicitly enabled local PHP CLI server receiving loopback traffic. Forwarded headers supplied by clients are not trusted; TLS termination requires correct hosting configuration.

The package root defaults to the PHP directory. TYTAN_UPDATE_ROOT can configure an alternative readable directory. bootstrap returns its canonical path.

Source: `server/common.php`

```php
function bootstrap(): string
{
    // Log server errors without exposing filesystem paths or PHP warnings to clients.
    ini_set('display_errors', '0');
    set_exception_handler(function (Throwable $error): void {
        // Keep details in the private server log, while the response remains generic.
        error_log(sprintf(
            'Tytan SQLupdate endpoint failure: %s: %s in %s:%d',
            get_class($error),
            $error->getMessage(),
            $error->getFile(),
            $error->getLine()
        ));
        if (!headers_sent()) {
            fail_request(500, 'The server could not complete the request.');
        }
        exit;
    });

    $method = $_SERVER['REQUEST_METHOD'] ?? '';
    if ($method !== 'GET' && $method !== 'HEAD') {
        header('Allow: GET, HEAD');
        fail_request(405, 'Only GET and HEAD requests are supported.');
    }

    $https = (!empty($_SERVER['HTTPS']) && strtolower((string) $_SERVER['HTTPS']) !== 'off') ||
        (string) ($_SERVER['SERVER_PORT'] ?? '') === '443';
    $localTest = PHP_SAPI === 'cli-server' && getenv('TYTAN_ALLOW_LOCAL_HTTP') === '1' &&
        in_array($_SERVER['REMOTE_ADDR'] ?? '', ['127.0.0.1', '::1'], true);

    // The HTTP exception works only for explicitly enabled local CLI-server tests.
    if (!$https && !$localTest) {
        fail_request(403, 'HTTPS is required.');
    }

    authenticate();

    $configuredRoot = getenv('TYTAN_UPDATE_ROOT');
    $base = realpath($configuredRoot === false || $configuredRoot === '' ? __DIR__ : $configuredRoot);
    if ($base === false || !is_dir($base) || !is_readable($base)) {
        fail_request(503, 'The update directory is not available.');
    }

    return $base;
}
```

Authentication has two modes. When neither PHP credential variable exists, the script requires REMOTE_USER set by the hosting's directory authentication. When PHP credentials are configured, both variables must be nonempty and the request credentials must match. Credentials can come from PHP_AUTH_USER/PHP_AUTH_PW or the hosting's forwarded Basic Authorization header. Strict base64 decoding and hash_equals are used for checking.

Absent or incomplete server authentication configuration returns 503. Wrong request credentials in PHP-managed mode return 401 with a BasicAuth challenge. Hosting authentication may reject a request before PHP and return its own HTML error page.

The supplied shared account can access every client folder under the root. A client-folder parameter does not create per-customer authorization. Direct ZIP URLs must remain protected by hosting authentication, or the package root should be outside the public document root when using PHP-managed authentication.

Source: `server/common.php`

```php
function authenticate(): void
{
    $configuredUser = getenv('TYTAN_API_USERNAME');
    $configuredPassword = getenv('TYTAN_API_PASSWORD');

    // Existing hosting BasicAuth may authenticate before PHP runs.
    // REMOTE_USER must be populated by the trusted web server, never by a header.
    if ($configuredUser === false && $configuredPassword === false) {
        if (!empty($_SERVER['REMOTE_USER'])) {
            return;
        }
        fail_request(503, 'Authentication is not configured on the server.');
    }

    if (!is_string($configuredUser) || $configuredUser === '' ||
        !is_string($configuredPassword) || $configuredPassword === '') {
        fail_request(503, 'Authentication configuration is incomplete.');
    }

    $user = $_SERVER['PHP_AUTH_USER'] ?? '';
    $password = $_SERVER['PHP_AUTH_PW'] ?? '';
    $authorization = $_SERVER['HTTP_AUTHORIZATION'] ?? $_SERVER['REDIRECT_HTTP_AUTHORIZATION'] ?? '';

    if ($authorization !== '') {
        $user = $password = '';
        if (preg_match('/^Basic\s+([A-Za-z0-9+\/=]+)$/i', $authorization, $matches)) {
            $decoded = base64_decode($matches[1], true);
            if ($decoded !== false && strpos($decoded, ':') !== false) {
                [$user, $password] = explode(':', $decoded, 2);
            }
        }
    }

    $userMatches = hash_equals($configuredUser, $user);
    $passwordMatches = hash_equals($configuredPassword, $password);

    if (!$userMatches || !$passwordMatches) {
        header('WWW-Authenticate: Basic realm="Tytan SQLupdate", charset="UTF-8"');
        fail_request(401, 'Authentication required or credentials rejected.');
    }
}
```

## 7. common.php: path validation and containment

query_path accepts only a string parameter. An empty dir is permitted for a root listing; an empty file is rejected. Each slash-separated segment must pass public_name. This rejects dot-prefixed segments, traversal, empty components, control characters, backslashes, colons, and other invalid filename characters, including trailing spaces or dots.

resolve_target uses realpath to resolve the actual filesystem target, then inside_root requires equality with the root or a prefix containing the directory separator. SQLupdate-other therefore cannot match SQLupdate. Canonical resolution also detects links to an outside directory. Nonexistent or outside targets return 404.

Source: `server/common.php`

```php
function query_path(string $parameter, bool $allowEmpty): string
{
    $value = $_GET[$parameter] ?? '';
    if (!is_string($value)) {
        fail_request(400, 'Invalid path parameter.');
    }
    if ($value === '' && $allowEmpty) {
        return '';
    }
    if ($value === '') {
        fail_request(400, 'A file path is required.');
    }

    foreach (explode('/', $value) as $segment) {
        if (!public_name($segment)) {
            fail_request(400, 'Invalid relative path.');
        }
    }

    return $value;
}

function public_name(string $name): bool
{
    // Dotfiles, traversal, absolute paths and Windows path separators are excluded.
    return $name !== '' && $name[0] !== '.' &&
        !preg_match('/[\x00-\x1F\x7F<>:"\\\\|?*\/]/', $name) &&
        !in_array(substr($name, -1), ['.', ' '], true);
}

function inside_root(string $path, string $base): bool
{
    // A separator boundary prevents SQLupdate-other from matching SQLupdate.
    // strpos(... ) === 0 provides prefix matching on both PHP 7.2 and PHP 8.
    return $path === $base || strpos($path, rtrim($base, '/\\') . DIRECTORY_SEPARATOR) === 0;
}

function resolve_target(string $base, string $relative): string
{
    $target = realpath($base . DIRECTORY_SEPARATOR . $relative);
    if ($target === false || !inside_root($target, $base)) {
        fail_request(404, 'File or directory not found.');
    }

    return $target;
}
```

## 8. common.php: JSON and errors

send_json encodes an array with Unicode and slash escaping disabled. PHP 7.2 does not have JSON_THROW_ON_ERROR, so encoding failures are detected by checking the false return value and raising a RuntimeException. fail_request constructs the error object. Responses include status, JSON content type, byte length, no-store, and nosniff. HEAD emits headers and skips the body.

Source: `server/common.php`

```php
function send_json(array $data, int $status = 200): void
{
    // PHP 7.2 has no JSON_THROW_ON_ERROR; explicitly check encoding failures.
    $json = json_encode($data, JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES);
    if ($json === false) {
        throw new RuntimeException('JSON encoding failed: ' . json_last_error_msg());
    }

    http_response_code($status);
    header('Content-Type: application/json; charset=utf-8');
    header('Cache-Control: no-store');
    header('X-Content-Type-Options: nosniff');
    header('Content-Length: ' . strlen($json));

    if (($_SERVER['REQUEST_METHOD'] ?? '') !== 'HEAD') {
        echo $json;
    }
    exit;
}

function fail_request(int $status, string $message): void
{
    send_json(['error' => $message], $status);
}
```

| HTTP status | Meaning |
| --- | --- |
| 200 | Listing or download succeeded |
| 400 | Invalid parameter or missing required file path |
| 401 | Missing or incorrect request credentials in PHP-managed authentication |
| 403 | HTTPS required |
| 404 | Missing/unreadable target, outside root, wrong type, or non-ZIP download |
| 405 | Unsupported method; Allow header specifies GET and HEAD |
| 500 | Endpoint operation or unhandled exception failed |
| 503 | Server credentials or package root unavailable/misconfigured |

Example endpoint error:

```json
{"error":"The server could not complete the request."}
```

Direct common.php requests return a plain 404. Hosting-generated authentication and PHP startup errors are not guaranteed to use the endpoint JSON contract.

## 9. Configuration and PHP 7.2 compatibility

| Setting | Purpose |
| --- | --- |
| TYTAN_API_USERNAME | Private username for PHP-managed BasicAuth |
| TYTAN_API_PASSWORD | Private password for PHP-managed BasicAuth |
| TYTAN_UPDATE_ROOT | Optional package root, defaulting to the PHP directory |
| TYTAN_ALLOW_LOCAL_HTTP | Test-only switch for loopback CLI server requests |

The hosting runtime was confirmed by the user as PHP 7.2.34 without str_starts_with. The initial implementation used that PHP 8 function and JSON_THROW_ON_ERROR, which is also unavailable in PHP 7.2. The initial authenticated API request returned HTTP 500. common.php was corrected to use strpos for prefix checking and explicit JSON failure handling; private error logging was also expanded. The user then supplied successful listings and reported working downloads.

The corrected files have passed syntax and endpoint checks on PHP 7.2.34 and PHP 8.5.11. Credentials are never hardcoded in the delivered files or documentation. The existing working .htaccess did not need modification for this PHP compatibility correction.

## 10. URLs and integration with Tytan

```text
https://tytan.poznan.pl/SQLupdate/api.php
https://tytan.poznan.pl/SQLupdate/api.php?dir=Barcin_Wodbar
https://tytan.poznan.pl/SQLupdate/download.php?file=Barcin_Wodbar/Faktury_008.000.043.zip
https://tytan.poznan.pl/SQLupdate/download.php?file=Barcin_Wodbar/FK2025_005.005.007.zip
https://tytan.poznan.pl/SQLupdate/download.php?file=Barcin_Wodbar/FK2026_005.005.040.zip
```

Tytan authenticates and queries its client folder. It filters packages by the desired product, compares numeric version components against its installed version, and downloads through download.php only when appropriate. Example: Faktury installed at 008.000.042 selects Faktury_008.000.043.zip. An equal or higher installed version should not download this update. FK2025, FK2026, and Faktury are separate products; UTC modification dates do not establish version precedence.

Use an HTTP library to encode query values and handle authentication and HTTP failures. Validate downloaded size and archive integrity before installation. File size checks do not prove authenticity; no signed manifest or server-side checksum is provided by this implementation. Tytan applies the package and records the installed version after success. These client responsibilities are not executed by the PHP scripts.

## 11. Observed production results

The following root listing was supplied by the user after deploying the compatibility fix:

| Name | Type | Size | Modified (UTC) |
| --- | --- | --- | --- |
| Barcin_Wodbar | folder | null | 2026-10-02 06:57:35 |
| barczewo_zwik | folder | null | 2026-10-02 06:57:44 |

The client listing for Barcin_Wodbar contained:

| Package | Size (bytes) | Modified (UTC) |
| --- | --- | --- |
| FK2025_005.005.007.zip | 11006463 | 2026-10-02 06:57:35 |
| FK2026_005.005.040.zip | 26190268 | 2026-10-02 06:57:38 |
| Faktury_008.000.043.zip | 17492922 | 2026-10-02 06:57:29 |

Each entry has type file and a relative path consisting of Barcin_Wodbar/ plus the package name. These values are copied from the actual JSON supplied by the user, not illustrative test fixtures.

The user subsequently confirmed that everything works after the download check. Production download success is user-reported. The agent has not independently measured the production downloaded bytes, checksum, archive integrity, or concurrency. Tytan's actual installed-version comparison and installation still require integration acceptance.

Changing transport to HTTPS and passing functional tests do not establish a solution to the original saturation incident. The repository contains no load test, CDN, queue, download throttling, or capacity measurement.

## 12. Automated tests and maintenance

The Python standard-library harness copies the three PHP files into a temporary package tree, creates sample ZIPs, starts PHP's built-in server on loopback, and executes HTTP checks. All test credentials and packages are temporary. It stops the servers and cleans up the temporary tree, and never contacts production.

```powershell
python tests/server/test_endpoints.py --php C:/path/to/php.exe
```

The current harness passes 37 checks on PHP 7.2.34 and PHP 8.5.11 in this Windows environment. Coverage includes syntax, authentication failures, method checks, root/client/empty listings, invalid parameters, traversal, non-ZIP requests, missing files, byte-for-byte downloads, headers, HEAD, names containing spaces/ampersands, root-prefix boundaries, invalid UTF-8 JSON error handling, and configuration failures. A filesystem symlink escape case is skipped if the operating system does not allow creating symlinks; the sibling-prefix check always runs. The tests do not validate production Apache configuration or concurrent load.

The former C# prototype tests passed 17/17 before removal; this is historical evidence and no longer a runnable part of the current repository. The retained PHP test command needs Python and PHP only. The new desktop application has separate checks documented in desktop.md.

To regenerate this Word document from the Markdown source:

```powershell
python -m pip install -r scripts/requirements-docs.txt
python scripts/export_technical_doc.py
```

For operational details use server.md and uso.md; verificacion.md retains the chronological evidence and fases.md records the delivery phases.
