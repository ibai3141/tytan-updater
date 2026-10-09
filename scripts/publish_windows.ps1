param(
    [ValidateSet('win-x64', 'win-arm64')]
    [string]$Runtime = 'win-x64'
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$releaseId = 'TytanUpdater-' + $Runtime + '-' + (Get-Date -Format 'yyyyMMdd-HHmmss') + '-' + [guid]::NewGuid().ToString('N').Substring(0, 6)
$buildRoot = Join-Path $repoRoot ('downloads/release-builds/' + $releaseId)
$releaseRoot = Join-Path $repoRoot ('dist/' + $releaseId)

# Every run owns new directories; never delete or overwrite another release.
New-Item -ItemType Directory -Path $releaseRoot | Out-Null
$projects = @(
    @{ Project = 'desktop/Tytan.Updater.Desktop'; Name = 'Tytan.Updater.Desktop' },
    @{ Project = 'cli/Tytan.Updater.Cli'; Name = 'Tytan.Updater.Cli' }
)

foreach ($app in $projects) {
    $publishRoot = Join-Path $buildRoot $app.Name
    $projectPath = Join-Path $repoRoot $app.Project

    # Bundle the runtime and native libraries; keep WinForms untrimmed.
    & dotnet publish $projectPath --configuration Release --runtime $Runtime --self-contained true `
        -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
        -p:PublishTrimmed=false -p:DebugType=None -p:DebugSymbols=false --output $publishRoot
    if ($LASTEXITCODE -ne 0) {
        throw ('Publishing failed: ' + $app.Name + '. No delivery ZIP was created.')
    }

    $executable = Join-Path $publishRoot ($app.Name + '.exe')
    if (-not (Test-Path -LiteralPath $executable -PathType Leaf)) {
        throw ('Published executable not found: ' + $executable)
    }
    Copy-Item -LiteralPath $executable -Destination $releaseRoot
}

Copy-Item -LiteralPath (Join-Path $repoRoot 'examples/installation.example.json') -Destination $releaseRoot
Copy-Item -LiteralPath (Join-Path $repoRoot 'docs/distribution-readme.txt') -Destination (Join-Path $releaseRoot 'README.txt')

$zipPath = $releaseRoot + '.zip'
Compress-Archive -Path (Join-Path $releaseRoot '*') -DestinationPath $zipPath -CompressionLevel Optimal
Get-FileHash -LiteralPath $zipPath -Algorithm SHA256 | Format-List
Write-Output ('Delivery directory: ' + $releaseRoot)
Write-Output ('Delivery ZIP: ' + $zipPath)
