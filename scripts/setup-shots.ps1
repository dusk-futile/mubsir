# Opens the setup window in each stage on a Windows desktop and takes a picture. It uses --setup-demo, which has no keys and makes no calls.
param([Parameter(Mandatory)][string]$Exe, [Parameter(Mandatory)][string]$Out)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing, System.Windows.Forms
New-Item -ItemType Directory -Force -Path $Out | Out-Null
foreach ($stage in 'keys', 'keys-ar', 'questions', 'questions-ar', 'settings', 'settings-ar') {
    $p = Start-Process -FilePath $Exe -ArgumentList '--setup-demo', $stage -PassThru
    Start-Sleep -Seconds 6
    if ($p.HasExited) { throw "the setup window closed itself in stage $stage, exit code $($p.ExitCode)" }
    $size = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds
    $bitmap = New-Object System.Drawing.Bitmap $size.Width, $size.Height
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $graphics.CopyFromScreen(0, 0, 0, 0, $bitmap.Size)
    $bitmap.Save((Join-Path $Out "setup-$stage.png"), [System.Drawing.Imaging.ImageFormat]::Png)
    $graphics.Dispose(); $bitmap.Dispose()
    Stop-Process -Id $p.Id -Force
    Start-Sleep -Milliseconds 500
}
