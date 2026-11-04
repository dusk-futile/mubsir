# Runs the native notch and Hudhud (Mubsir.exe --overlay-demo) on a real Windows desktop and records what
# happens: a screenshot every second, the undo path with a real Ctrl press, memory and CPU, and a clean exit.
# Used by CI on windows-2025 (software rendering, no GPU, so its CPU numbers are a worst case).
#
#   pwsh scripts/overlay-smoke.ps1 -Exe publish/Mubsir.exe -Out overlay-shots

param(
    [string]$Exe = 'publish/Mubsir.exe',
    [string]$Out = 'overlay-shots'
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.Windows.Forms
Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
public static class SmokeInput {
    [DllImport("user32.dll")] public static extern void keybd_event(byte vk, byte scan, uint flags, UIntPtr extra);
    public static void Press(byte vk) {
        keybd_event(vk, 0, 0, UIntPtr.Zero);
        System.Threading.Thread.Sleep(40);
        keybd_event(vk, 0, 2, UIntPtr.Zero);
    }
}
'@

$VkControl = 0x11
$VkEscape = 0x1B
New-Item -ItemType Directory -Force -Path $Out | Out-Null
$numbers = Join-Path $Out 'numbers.txt'
Set-Content -Path $numbers -Value "Overlay smoke test, $([Environment]::OSVersion.VersionString)"

function Note([string]$text) {
    Write-Host $text
    Add-Content -Path $numbers -Value $text
}

function Shot([string]$name) {
    $bounds = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds
    $bitmap = New-Object System.Drawing.Bitmap $bounds.Width, $bounds.Height
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    # CaptureBlt also picks up layered (transparent) windows like the overlay's.
    $mode = [System.Drawing.CopyPixelOperation]::SourceCopy -bor [System.Drawing.CopyPixelOperation]::CaptureBlt
    $graphics.CopyFromScreen($bounds.Location, [System.Drawing.Point]::Empty, $bounds.Size, $mode)
    $bitmap.Save((Join-Path $Out "$name.png"), [System.Drawing.Imaging.ImageFormat]::Png)
    $graphics.Dispose()
    $bitmap.Dispose()
}

function Assert-Running($process, [string]$when) {
    if ($process.HasExited) {
        throw "the overlay demo exited $when with code $($process.ExitCode)"
    }
}

function Measure-Cpu($process, [int]$seconds) {
    $process.Refresh()
    $before = $process.TotalProcessorTime.TotalSeconds
    Start-Sleep -Seconds $seconds
    $process.Refresh()
    return [math]::Round((($process.TotalProcessorTime.TotalSeconds - $before) / $seconds) * 100, 1)
}

function Memory-Mb($process) {
    $process.Refresh()
    return [math]::Round($process.WorkingSet64 / 1MB, 1)
}

$screen = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds
Note "Screen: $($screen.Width) x $($screen.Height)"

# First run: the whole story, then Hudhud falls asleep and Ctrl wakes it.
$demo = Start-Process -FilePath $Exe -ArgumentList '--overlay-demo' -PassThru
for ($t = 1; $t -le 30; $t++) {
    Start-Sleep -Seconds 1
    Assert-Running $demo "at $t s"
    Shot ('story-{0:d2}' -f $t)
}

Note "Memory after the story: $(Memory-Mb $demo) MB"
Note "CPU, awake and idle: $(Measure-Cpu $demo 5) % of one core"

# It sleeps 20 s after the last activity.
Start-Sleep -Seconds 12
Assert-Running $demo 'while falling asleep'
Shot 'asleep'
Note "CPU, asleep: $(Measure-Cpu $demo 5) % of one core"
Note "Memory, asleep: $(Memory-Mb $demo) MB"

[SmokeInput]::Press($VkControl)
Start-Sleep -Milliseconds 700
Shot 'woken-shake'
Start-Sleep -Seconds 2
Shot 'woken'
Assert-Running $demo 'after waking'

[SmokeInput]::Press($VkEscape)
if (-not $demo.WaitForExit(5000)) {
    throw 'the overlay demo did not exit after Esc'
}
Note "Exit code after Esc: $($demo.ExitCode)"
if ($demo.ExitCode -ne 0) {
    throw "the overlay demo exited with code $($demo.ExitCode)"
}

# Second run: Ctrl in the middle of the checklist stops it and undoes the finished steps.
$demo = Start-Process -FilePath $Exe -ArgumentList '--overlay-demo' -PassThru
Start-Sleep -Milliseconds 7900
Assert-Running $demo 'before the undo test'
Shot 'doing-before-ctrl'
[SmokeInput]::Press($VkControl)
foreach ($ms in 300, 500, 500, 500, 700, 1000, 1500, 1500) {
    Start-Sleep -Milliseconds $ms
    Shot ('undo-{0:d2}' -f (Get-ChildItem $Out -Filter 'undo-*.png').Count)
}
Assert-Running $demo 'after the undo test'
[SmokeInput]::Press($VkEscape)
if (-not $demo.WaitForExit(5000)) {
    throw 'the overlay demo did not exit after Esc (second run)'
}

Note 'Smoke test passed.'
