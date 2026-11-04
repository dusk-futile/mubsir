# Runs the native notch and Hudhud (Mubsir.exe --overlay-demo) on a real Windows desktop and records what
# happens: a screenshot every second, the undo path with a real Ctrl press, memory and CPU, and a clean exit.
# Used by CI on windows-2025 (software rendering, no GPU, so its CPU numbers are a worst case).
#
#   pwsh scripts/overlay-smoke.ps1 -Exe publish/Mubsir.exe -Out overlay-shots
#
# -ForceAnimations turns on Windows' "Show animations" for this user first, because CI machines have it
# off and the overlay then rightly stays still. It changes a system setting, so only CI passes it.

param(
    [string]$Exe = 'publish/Mubsir.exe',
    [string]$Out = 'overlay-shots',
    [switch]$ForceAnimations
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

    [DllImport("user32.dll")] static extern IntPtr GetDC(IntPtr hwnd);
    [DllImport("user32.dll")] static extern int ReleaseDC(IntPtr hwnd, IntPtr dc);
    [DllImport("gdi32.dll")] static extern bool BitBlt(IntPtr dst, int x, int y, int w, int h, IntPtr src, int sx, int sy, uint rop);
    [DllImport("user32.dll")] static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] static extern void mouse_event(uint flags, int dx, int dy, uint data, UIntPtr extra);

    // Moves there, gives the overlay time to notice the cursor is on it, then clicks.
    public static void ClickAt(int x, int y) {
        SetCursorPos(x, y);
        System.Threading.Thread.Sleep(400);
        mouse_event(0x0002, 0, 0, 0, UIntPtr.Zero);
        System.Threading.Thread.Sleep(50);
        mouse_event(0x0004, 0, 0, 0, UIntPtr.Zero);
    }

    [DllImport("user32.dll", EntryPoint = "SystemParametersInfoW")] static extern bool SystemParametersInfo(uint action, uint param, IntPtr value, uint flags);

    public static void TurnOnAnimations() {
        const uint SetClientAreaAnimation = 0x1043;
        const uint SendChange = 0x0002;
        SystemParametersInfo(SetClientAreaAnimation, 0, new IntPtr(1), SendChange);
    }

    const uint SrcCopy = 0x00CC0020;
    const uint CaptureBlt = 0x40000000;

    // CaptureBlt also picks up layered (transparent) windows like the overlay's.
    public static void CopyScreen(IntPtr target, int width, int height) {
        IntPtr screen = GetDC(IntPtr.Zero);
        BitBlt(target, 0, 0, width, height, screen, 0, 0, SrcCopy | CaptureBlt);
        ReleaseDC(IntPtr.Zero, screen);
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
    $target = $graphics.GetHdc()
    [SmokeInput]::CopyScreen($target, $bounds.Width, $bounds.Height)
    $graphics.ReleaseHdc($target)
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
    return "$([math]::Round($process.WorkingSet64 / 1MB, 1)) MB working set, $([math]::Round($process.PrivateMemorySize64 / 1MB, 1)) MB private"
}

$screen = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds
Note "Screen: $($screen.Width) x $($screen.Height)"
try {
    Add-Type -AssemblyName PresentationFramework
    Note "Windows 'Show animations' at the start: $([System.Windows.SystemParameters]::ClientAreaAnimation)"
    if ($ForceAnimations) {
        [SmokeInput]::TurnOnAnimations()
        Note 'Turned on for this run.'
    }
    Note "WPF render tier: $([System.Windows.Media.RenderCapability]::Tier -shr 16) (0 = software)"
}
catch {
    Note "Could not read the animation setting: $($_.Exception.Message)"
}

# First run: the whole story, then Hudhud falls asleep and Ctrl wakes it.
$demo = Start-Process -FilePath $Exe -ArgumentList '--overlay-demo' -PassThru
for ($t = 1; $t -le 36; $t++) {
    Start-Sleep -Seconds 1
    Assert-Running $demo "at $t s"
    Shot ('story-{0:d2}' -f $t)
}

Note "Memory after the story: $(Memory-Mb $demo)"
Note "CPU, awake and idle: $(Measure-Cpu $demo 5) % of one core"

# It sleeps 20 s after the last activity.
Start-Sleep -Seconds 16
Assert-Running $demo 'while falling asleep'
Shot 'asleep'
Note "CPU, asleep: $(Measure-Cpu $demo 5) % of one core"
Note "Memory, asleep: $(Memory-Mb $demo)"

[SmokeInput]::Press($VkControl)
Start-Sleep -Milliseconds 500
Shot 'woken-shake-1'
Start-Sleep -Milliseconds 150
Shot 'woken-shake-2'
Start-Sleep -Seconds 2
Shot 'woken'
Assert-Running $demo 'after waking'

# A click on the bird opens the panel, and a click anywhere else closes it again.
$work = [System.Windows.Forms.Screen]::PrimaryScreen.WorkingArea
$birdX = $work.Right - 28 - 46
$birdY = $work.Bottom - 2 - 42
Shot 'before-click'
[SmokeInput]::ClickAt($birdX, $birdY)
Start-Sleep -Milliseconds 800
Shot 'panel-open'
[SmokeInput]::ClickAt(120, 300)
Start-Sleep -Milliseconds 800
Shot 'panel-closed'
Assert-Running $demo 'after the panel'

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
