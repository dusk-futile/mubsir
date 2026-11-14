# Builds the release zip: the app, the NVDA add-on, the installer script, the licence and a plain-text
# start guide, plus a SHA-256 file. The release workflow runs this; run it yourself to get the same zip.
#
#   powershell -File scripts/package.ps1 -Version v1.0.0   (Windows PowerShell 5.1 or pwsh 7)
#
# Output: out/Mubsir-<version>-win-x64.zip and out/Mubsir-<version>-win-x64.zip.sha256
[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidatePattern('^v\d+\.\d+\.\d+([-.][0-9A-Za-z.]+)?$')]
    [string]$Version,
    [string]$Out = 'out'
)

$ErrorActionPreference = 'Stop'
function Check([string]$what) { if ($LASTEXITCODE -ne 0) { throw "$what failed with exit code $LASTEXITCODE" } }
Set-Location (Split-Path $PSScriptRoot -Parent)

$name = "Mubsir-$Version-win-x64"
$root = Join-Path $Out $name
if (Test-Path $root) { Remove-Item $root -Recurse -Force }

dotnet restore src/Mubsir.App; Check 'restore'
dotnet publish src/Mubsir.App -c Release -r win-x64 --no-restore -o "$root/Mubsir"; Check 'publish'
if (-not (Test-Path "$root/Mubsir/Mubsir.exe")) { throw 'Mubsir.exe was not published' }

python scripts/build-addon.py --out-dir "$root/Mubsir"; Check 'add-on build'
Copy-Item (Join-Path "$root/Mubsir" '*.nvda-addon') $root
Copy-Item scripts/setup.ps1, LICENSE, docs/START-HERE.txt $root

$zip = Join-Path $Out "$name.zip"
if (Test-Path $zip) { Remove-Item $zip -Force }
# Entries are written one by one with forward slashes: Compress-Archive and ZipFile in Windows PowerShell 5.1
# write backslashes, which some unzip tools turn into odd file names.
Add-Type -AssemblyName System.IO.Compression, System.IO.Compression.FileSystem
$full = (Resolve-Path $root).Path
$file = [IO.File]::Create((Join-Path (Resolve-Path $Out) "$name.zip"))
$archive = New-Object IO.Compression.ZipArchive($file, [IO.Compression.ZipArchiveMode]::Create)
try {
    foreach ($item in Get-ChildItem $full -Recurse -File) {
        $entry = $item.FullName.Substring($full.Length + 1).Replace('\', '/')
        [void][IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive, $item.FullName, $entry, [IO.Compression.CompressionLevel]::Optimal)
    }
}
finally {
    $archive.Dispose()
    $file.Dispose()
}
$hash = (Get-FileHash $zip -Algorithm SHA256).Hash.ToLower()
[IO.File]::WriteAllText((Join-Path (Resolve-Path $Out) "$name.zip.sha256"), "$hash  $name.zip`n")

$mb = [math]::Round((Get-Item $zip).Length / 1MB, 1)
Write-Host "Built $zip ($mb MB)"
if ($env:GITHUB_ENV) { "ASSET=$name" | Out-File $env:GITHUB_ENV -Append }
