# Runs the native notch (Mubsir.exe --overlay-demo) on a real Windows desktop and records what
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

    public static void MoveTo(int x, int y) {
        SetCursorPos(x, y);
    }

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

    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern IntPtr FindWindow(string className, string title);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr hwnd);

    [DllImport("shell32.dll")] static extern int SHQueryUserNotificationState(out int state);

    // 2, 3 and 4 mean a full-screen app has the screen, which is what makes the overlay step aside.
    public static int NotificationState() {
        int state;
        SHQueryUserNotificationState(out state);
        return state;
    }

    public static bool IsVisible(string title) {
        return IsWindowVisible(FindWindow(null, title));
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

# The notch is pure black, so a black pixel at this spot means the notch reaches it.
function Test-NotchAt([string]$name, [int]$x, [int]$y) {
    $bitmap = New-Object System.Drawing.Bitmap (Join-Path $Out "$name.png")
    $c = $bitmap.GetPixel($x, $y)
    $bitmap.Dispose()
    return ($c.R -lt 4 -and $c.G -lt 4 -and $c.B -lt 4)
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

# The app itself must still start normally with WPF in the package.
$version = Start-Process -FilePath $Exe -ArgumentList '--version' -PassThru -Wait
if ($version.ExitCode -ne 0) {
    throw "Mubsir.exe --version exited with code $($version.ExitCode)"
}

# First run: the whole story, then the notch rests.
$demo = Start-Process -FilePath $Exe -ArgumentList '--overlay-demo' -PassThru -RedirectStandardOutput (Join-Path $Out 'demo-log.txt')
for ($t = 1; $t -le 36; $t++) {
    Start-Sleep -Seconds 1
    Assert-Running $demo "at $t s"
    Shot ('story-{0:d2}' -f $t)
}

Note "Memory after the story: $(Memory-Mb $demo)"
Note "CPU, awake and idle: $(Measure-Cpu $demo 5) % of one core"

# It rests 20 s after the last activity.
Start-Sleep -Seconds 16
Assert-Running $demo 'while resting'
Shot 'resting'
Note "CPU, resting: $(Measure-Cpu $demo 5) % of one core"
Note "Memory, resting: $(Memory-Mb $demo)"

# A full-screen app has the screen: the notch steps aside while there is nothing to show, then comes back.
$cover = New-Object System.Windows.Forms.Form
$cover.FormBorderStyle = 'None'
$cover.WindowState = 'Maximized'
$cover.TopMost = $true
$cover.BackColor = [System.Drawing.Color]::DarkSlateGray
$cover.Show()
$cover.Activate()
for ($i = 0; $i -lt 40; $i++) {
    [System.Windows.Forms.Application]::DoEvents()
    Start-Sleep -Milliseconds 100
}
$hiddenOver = -not [SmokeInput]::IsVisible('Mubsir notch')
Note "Windows notification state over the test window: $([SmokeInput]::NotificationState()) (2, 3 or 4 = full-screen)"
$cover.Close()
$cover.Dispose()
for ($i = 0; $i -lt 40; $i++) {
    [System.Windows.Forms.Application]::DoEvents()
    Start-Sleep -Milliseconds 100
}
$backAfter = [SmokeInput]::IsVisible('Mubsir notch')
Note "Full-screen app: notch hidden while it runs: $hiddenOver; back afterwards: $backAfter"
if (-not $backAfter) {
    throw 'the notch did not come back after the full-screen app closed'
}
Assert-Running $demo 'after the full-screen test'
$dpi = [System.Drawing.Graphics]::FromHwnd([IntPtr]::Zero).DpiX
if ($dpi -eq 96) {
    # Resting, the notch is 120 wide. Hovering it peeks (236 wide), and clicking it starts listening (404 wide).
    $middle = [int]($screen.Width / 2)
    [SmokeInput]::MoveTo(120, 300)
    Start-Sleep -Milliseconds 500
    Shot 'notch-resting'
    [SmokeInput]::MoveTo($middle, 8)
    Start-Sleep -Milliseconds 1000
    Shot 'notch-hover'
    if (Test-NotchAt 'notch-resting' ($middle - 100) 8) { throw 'the resting notch is wider than it should be' }
    if (-not (Test-NotchAt 'notch-hover' ($middle - 100) 8)) { throw 'hovering the notch did not make it peek' }
    [SmokeInput]::ClickAt($middle, 8)
    Start-Sleep -Milliseconds 1200
    Shot 'notch-clicked'
    if (-not (Test-NotchAt 'notch-clicked' ($middle - 180) 8)) { throw 'clicking the notch did not start listening' }
    Note 'Notch: peeks on hover, starts listening on a click.'
}

[SmokeInput]::Press($VkEscape)
if (-not $demo.WaitForExit(5000)) {
    throw 'the overlay demo did not exit after Esc'
}
Note "Exit code after Esc: $($demo.ExitCode)"
if ($demo.ExitCode -ne 0) {
    throw "the overlay demo exited with code $($demo.ExitCode)"
}

# The overlay survives its own errors, so the demo's log is the only place they would show.
$log = Get-Content (Join-Path $Out 'demo-log.txt') -Raw
$rates = ([regex]::Matches($log, '(\d+) frames a second') | ForEach-Object { $_.Groups[1].Value }) -join ', '
Note "Frames a second, every 5 s: $rates"
if ($log -match 'overlay error') {
    throw "the overlay reported an error: $log"
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
