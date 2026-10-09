[CmdletBinding(SupportsShouldProcess = $true, ConfirmImpact = 'Low')]
param()

$ErrorActionPreference = 'Stop'
$workspaceRoot = (Resolve-Path -LiteralPath (Split-Path -Parent $PSScriptRoot)).Path
$relativeTargets = @(
    'downloads/demo',
    'downloads/diagnostics',
    'downloads/dialog-check',
    'downloads/release-builds',
    'downloads/release-verification',
    'downloads/server-demo',
    'downloads/swagger-tools',
    'desktop/Tytan.Updater.Desktop/bin',
    'desktop/Tytan.Updater.Desktop/obj',
    'cli/Tytan.Updater.Cli/bin',
    'cli/Tytan.Updater.Cli/obj',
    'downloads/desktop-configured.png',
    'downloads/desktop-download-completion.png',
    'downloads/desktop-manual-input.png',
    'downloads/desktop-phase1.png',
    'downloads/desktop-phase2-live.png',
    'downloads/desktop-phase3-live.png',
    'downloads/desktop-phase4-live.png',
    'downloads/swagger-auth.png',
    'downloads/swagger-preview-tested.png',
    'downloads/swagger-preview.png',
    'downloads/swagger-test.zip'
)

# Validate the complete explicit allowlist before deleting anything.
# dist, customer downloads, source, examples, documentation and .git are excluded.
$verifiedTargets = @()
$plannedBytes = 0L
foreach ($relativeTarget in $relativeTargets) {
    $absoluteTarget = [IO.Path]::GetFullPath((Join-Path $workspaceRoot $relativeTarget))
    if (-not $absoluteTarget.StartsWith($workspaceRoot + [IO.Path]::DirectorySeparatorChar,
            [StringComparison]::OrdinalIgnoreCase)) {
        throw ('Cleanup target is outside the workspace: ' + $relativeTarget)
    }
    if (-not (Test-Path -LiteralPath $absoluteTarget)) { continue }
    $resolvedTarget = (Resolve-Path -LiteralPath $absoluteTarget).Path
    if ($resolvedTarget -ne $absoluteTarget) { throw ('Unexpected resolved path: ' + $relativeTarget) }
    $item = Get-Item -LiteralPath $resolvedTarget -Force
    if (($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
        throw ('Cleanup rejects linked targets: ' + $relativeTarget)
    }
    if ($item.PSIsContainer -and
        @(Get-ChildItem -LiteralPath $resolvedTarget -Recurse -Force -Attributes ReparsePoint).Count -gt 0) {
        throw ('Cleanup rejects nested linked paths: ' + $relativeTarget)
    }
    $trackedFiles = @(& git -C $workspaceRoot ls-files -- $relativeTarget)
    if ($LASTEXITCODE -ne 0 -or $trackedFiles.Count -gt 0) {
        throw ('Cleanup rejects tracked content or an unsuccessful Git check: ' + $relativeTarget)
    }
    $files = if ($item.PSIsContainer) {
        @(Get-ChildItem -LiteralPath $resolvedTarget -File -Recurse -Force)
    } else { @($item) }
    $plannedBytes += ($files | Measure-Object Length -Sum).Sum
    $verifiedTargets += $item
}

foreach ($item in $verifiedTargets) {
    if ($PSCmdlet.ShouldProcess($item.FullName, 'Remove generated development artifact')) {
        if ($item.PSIsContainer) {
            Remove-Item -LiteralPath $item.FullName -Recurse -Force
        } else {
            Remove-Item -LiteralPath $item.FullName -Force
        }
    }
}

if ($WhatIfPreference) {
    Write-Output ('Preview only. Candidate size: {0:N2} GiB. No files were deleted.' -f ($plannedBytes / 1GB))
} else {
    Write-Output ('Cleanup completed. Removed {0} generated targets ({1:N2} GiB of file data).' -f $verifiedTargets.Count, ($plannedBytes / 1GB))
}
