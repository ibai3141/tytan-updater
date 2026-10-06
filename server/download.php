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
