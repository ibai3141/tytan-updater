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

    // Match the lowercase JSON contract used by FileEntry in the C# client.
    $result[] = [
        'name' => $name,
        'type' => $folder ? 'folder' : 'file',
        'size' => $size,
        'modified' => gmdate('Y-m-d H:i:s', $modified),
        'path' => ($relative === '' ? '' : $relative . '/') . $name,
    ];
}

send_json($result);
