<?php
declare(strict_types=1);

// Shared endpoint logic. This file is not an API endpoint itself.
if (!defined('TYTAN_ENDPOINT')) {
    http_response_code(404);
    exit;
}

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
