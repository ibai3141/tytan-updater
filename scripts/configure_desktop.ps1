param(
    [Parameter(Mandatory = $true)][string]$ClientFolder,
    [Parameter(Mandatory = $true)][string]$InstalledFolder,
    [string]$OutputPath = (Join-Path $env:LOCALAPPDATA 'TytanUpdater/folders.json')
)

$ErrorActionPreference = 'Stop'
function Test-FolderName([string]$Name) {
    return -not [string]::IsNullOrWhiteSpace($Name) -and -not $Name.StartsWith('.') `
        -and $Name -eq $Name.TrimEnd([char[]]@(' ', '.')) `
        -and $Name -notmatch '[\x00-\x1F\x7F<>:"\\|?*/]'
}

# The administrator supplies real customer/version information, never credentials.
if (-not (Test-FolderName $ClientFolder)) { throw 'Invalid client folder name.' }
if (-not (Test-FolderName $InstalledFolder) -or $InstalledFolder -notmatch '^(.+)_([0-9]{3}\.[0-9]{3}\.[0-9]{3})$') {
    throw 'Installed folder must have a name such as Faktury_008.000.042.'
}
if (-not (Test-FolderName $Matches[1])) { throw 'Invalid product name.' }

$target = [IO.Path]::GetFullPath($OutputPath)
[IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($target)) | Out-Null
$temporary = $target + '.' + [guid]::NewGuid().ToString('N') + '.tmp'
try {
    $json = @{ ClientFolder = $ClientFolder; InstalledFolder = $InstalledFolder } | ConvertTo-Json
    [IO.File]::WriteAllText($temporary, $json, [Text.UTF8Encoding]::new($false))
    if ([IO.File]::Exists($target)) {
        [IO.File]::Replace($temporary, $target, [NullString]::Value)
    } else {
        [IO.File]::Move($temporary, $target)
    }
} finally {
    if ([IO.File]::Exists($temporary)) { [IO.File]::Delete($temporary) }
}
Write-Output ('Desktop client configured: ' + $target)
