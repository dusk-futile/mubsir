# Builds an unsigned Store package from a published app. The Store signs it on submission. See docs/STORE.md.
param([Parameter(Mandatory)][string]$Publish, [Parameter(Mandatory)][string]$Out)
$ErrorActionPreference = 'Stop'
$layout = Join-Path $Out 'layout'
New-Item -ItemType Directory -Force -Path (Join-Path $layout 'Assets') | Out-Null
Copy-Item -Recurse -Force (Join-Path $Publish '*') $layout
$hasAddon = (Get-ChildItem -LiteralPath $layout -Filter '*.nvda-addon' -File -ErrorAction SilentlyContinue).Count -gt 0
if (-not $hasAddon) {
    $existing = Get-ChildItem -Path 'dist', $PSScriptRoot, (Join-Path $PSScriptRoot '..') -Filter '*.nvda-addon' -Recurse -File -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($existing) {
        Copy-Item -Force $existing.FullName $layout
    } else {
        python (Join-Path $PSScriptRoot 'build-addon.py') --out-dir $layout
    }
}
Copy-Item -Force 'packaging/AppxManifest.xml' (Join-Path $layout 'AppxManifest.xml')
Add-Type -AssemblyName System.Drawing
$source = [System.Drawing.Image]::FromFile((Resolve-Path 'assets/brand/png/mubsir-512.png'))
foreach ($asset in @{ Name = 'Square44x44Logo'; Size = 44 }, @{ Name = 'Square150x150Logo'; Size = 150 }, @{ Name = 'StoreLogo'; Size = 50 }) {
    $bitmap = New-Object System.Drawing.Bitmap $asset.Size, $asset.Size
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $graphics.InterpolationMode = 'HighQualityBicubic'
    $graphics.DrawImage($source, 0, 0, $asset.Size, $asset.Size)
    $bitmap.Save((Join-Path $layout "Assets/$($asset.Name).png"), [System.Drawing.Imaging.ImageFormat]::Png)
    $graphics.Dispose(); $bitmap.Dispose()
}
$source.Dispose()
$tool = Get-ChildItem 'C:\Program Files (x86)\Windows Kits\10\bin\*\x64\makeappx.exe' | Sort-Object FullName | Select-Object -Last 1
if (-not $tool) { throw 'makeappx.exe was not found. It comes with the Windows SDK.' }
& $tool.FullName pack /d $layout /p (Join-Path $Out 'Mubsir.msix') /o
if ($LASTEXITCODE -ne 0) { throw "makeappx failed with $LASTEXITCODE" }
Remove-Item -Recurse -Force $layout
