<#
.SYNOPSIS
    Prints this PC's hardware and software inventory for Basira and saves it as inventory.json.

.DESCRIPTION
    Read-only. Collects CPU, memory, disks, Windows version, .NET runtimes, microphones,
    NVDA, Windows 10 ESU hints and Arabic speech/OCR support. It asks one question
    (does the keyboard have a Right Ctrl key?) unless -RightCtrl or -NonInteractive is given.
    No user names, serial numbers or file names are collected.

    Works in Windows PowerShell 5.1. Some details (Windows capabilities) need an
    administrator PowerShell; without it they are reported as "requires administrator".

.PARAMETER OutFile
    Where to write the JSON. Default: inventory.json in the current folder.

.PARAMETER Label
    A short label stored in the file, for example "father" or "test-pc".

.PARAMETER RightCtrl
    Answer for the Right Ctrl question: yes, no or unknown. Skips the prompt.

.PARAMETER UplinkTest
    Also measures download and upload speed (sends about 2 MB to speed.cloudflare.com)
    and the time to reach the AI services. Off by default.

.PARAMETER NonInteractive
    Never prompt; unanswered questions are recorded as "unknown".

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File .\scripts\inventory.ps1 -Label father
#>
[CmdletBinding()]
param(
    [string]$OutFile = (Join-Path (Get-Location).ProviderPath 'inventory.json'),
    [string]$Label = '',
    [ValidateSet('yes', 'no', 'unknown', '')]
    [string]$RightCtrl = '',
    [switch]$UplinkTest,
    [switch]$NonInteractive
)

$ErrorActionPreference = 'Stop'
$ScriptVersion = '1'

function Write-Section([string]$Title) {
    Write-Host ''
    Write-Host ('== ' + $Title) -ForegroundColor Cyan
}

function Write-Item([string]$Name, $Value) {
    if ($null -eq $Value -or "$Value" -eq '') { $Value = '(unknown)' }
    Write-Host ('  {0,-28} {1}' -f ($Name + ':'), $Value)
}

# Runs a collector and turns any failure into an "error" entry instead of stopping the script.
function Invoke-Collector([string]$Name, [scriptblock]$Collector) {
    try {
        return (& $Collector)
    }
    catch {
        Write-Warning ('{0}: {1}' -f $Name, $_.Exception.Message)
        return [ordered]@{ error = $_.Exception.Message }
    }
}

function Test-IsAdmin {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = New-Object Security.Principal.WindowsPrincipal($identity)
    return $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

function Get-RegistryValue([string]$Path, [string]$Name) {
    $item = Get-ItemProperty -Path $Path -Name $Name -ErrorAction SilentlyContinue
    if ($null -eq $item) { return $null }
    return $item.$Name
}

$isAdmin = Test-IsAdmin
Write-Host 'Basira PC inventory (read-only)' -ForegroundColor Green
if (-not $isAdmin) {
    Write-Host 'Not running as administrator: Windows capability details will be skipped.' -ForegroundColor Yellow
}

# --- Computer -----------------------------------------------------------------
$computer = Invoke-Collector 'computer' {
    $cs = Get-CimInstance -ClassName Win32_ComputerSystem
    $enclosure = Get-CimInstance -ClassName Win32_SystemEnclosure | Select-Object -First 1
    $laptopChassis = @(8, 9, 10, 11, 12, 14, 18, 21, 30, 31, 32)
    $chassis = @($enclosure.ChassisTypes)
    $hasBattery = [bool](Get-CimInstance -ClassName Win32_Battery -ErrorAction SilentlyContinue)
    $isLaptop = $hasBattery
    foreach ($type in $chassis) { if ($laptopChassis -contains [int]$type) { $isLaptop = $true } }
    [ordered]@{
        manufacturer = $cs.Manufacturer
        model        = $cs.Model
        chassisTypes = $chassis
        hasBattery   = $hasBattery
        likelyLaptop = $isLaptop
    }
}
Write-Section 'Computer'
Write-Item 'Model' ('{0} {1}' -f $computer.manufacturer, $computer.model)
Write-Item 'Laptop' $computer.likelyLaptop

# --- CPU ----------------------------------------------------------------------
$cpu = Invoke-Collector 'cpu' {
    $cpus = @(Get-CimInstance -ClassName Win32_Processor)
    [ordered]@{
        name        = ([string]$cpus[0].Name).Trim()
        sockets     = $cpus.Count
        cores       = [int](($cpus | Measure-Object -Property NumberOfCores -Sum).Sum)
        threads     = [int](($cpus | Measure-Object -Property NumberOfLogicalProcessors -Sum).Sum)
        maxClockMHz = $cpus[0].MaxClockSpeed
    }
}
Write-Section 'CPU'
Write-Item 'Name' $cpu.name
Write-Item 'Cores / threads' ('{0} / {1}' -f $cpu.cores, $cpu.threads)

# --- Memory -------------------------------------------------------------------
$memory = Invoke-Collector 'memory' {
    $cs = Get-CimInstance -ClassName Win32_ComputerSystem
    $installedBytes = (Get-CimInstance -ClassName Win32_PhysicalMemory | Measure-Object -Property Capacity -Sum).Sum
    $installedGB = $null
    if ($installedBytes) { $installedGB = [math]::Round($installedBytes / 1GB, 1) }
    [ordered]@{
        usableGB    = [math]::Round($cs.TotalPhysicalMemory / 1GB, 1)
        installedGB = $installedGB
    }
}
Write-Section 'Memory'
Write-Item 'Usable RAM (GB)' $memory.usableGB
Write-Item 'Installed RAM (GB)' $memory.installedGB

# --- Disks --------------------------------------------------------------------
$disks = Invoke-Collector 'disks' {
    $list = @()
    $physical = $null
    try { $physical = @(Get-PhysicalDisk -ErrorAction Stop | Sort-Object -Property DeviceId) } catch { $physical = $null }
    if ($physical) {
        foreach ($d in $physical) {
            $media = [string]$d.MediaType
            $guess = $media
            if ($media -eq 'Unspecified' -or $media -eq '') {
                # SpindleSpeed is 0 for SSDs and 4294967295 when unknown.
                if ([string]$d.BusType -eq 'NVMe' -or $d.SpindleSpeed -eq 0) { $guess = 'SSD (guessed)' }
                elseif ($d.SpindleSpeed -gt 0 -and $d.SpindleSpeed -lt 100000) { $guess = 'HDD (guessed)' }
                else { $guess = 'unknown' }
            }
            $list += [ordered]@{
                model     = $d.FriendlyName
                mediaType = $guess
                busType   = [string]$d.BusType
                sizeGB    = [math]::Round($d.Size / 1GB, 0)
            }
        }
    }
    else {
        foreach ($d in @(Get-CimInstance -ClassName Win32_DiskDrive)) {
            $list += [ordered]@{
                model     = $d.Model
                mediaType = 'unknown (Get-PhysicalDisk unavailable)'
                busType   = $d.InterfaceType
                sizeGB    = [math]::Round($d.Size / 1GB, 0)
            }
        }
    }
    $systemDrive = Get-CimInstance -ClassName Win32_LogicalDisk -Filter ("DeviceID='{0}'" -f $env:SystemDrive)
    [ordered]@{
        physical          = $list
        systemDrive       = $env:SystemDrive
        systemDriveFreeGB = [math]::Round($systemDrive.FreeSpace / 1GB, 1)
        systemDriveSizeGB = [math]::Round($systemDrive.Size / 1GB, 1)
    }
}
Write-Section 'Disks'
foreach ($d in @($disks.physical)) {
    Write-Item $d.model ('{0}, {1}, {2} GB' -f $d.mediaType, $d.busType, $d.sizeGB)
}
Write-Item ('Free on ' + $disks.systemDrive) ('{0} GB of {1} GB' -f $disks.systemDriveFreeGB, $disks.systemDriveSizeGB)

# --- Windows ------------------------------------------------------------------
$windows = Invoke-Collector 'windows' {
    $os = Get-CimInstance -ClassName Win32_OperatingSystem
    $cvPath = 'HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion'
    $displayVersion = Get-RegistryValue $cvPath 'DisplayVersion'
    if (-not $displayVersion) { $displayVersion = Get-RegistryValue $cvPath 'ReleaseId' }
    $build = [int]$os.BuildNumber
    $userLanguages = @()
    try { $userLanguages = @(Get-WinUserLanguageList | ForEach-Object { $_.LanguageTag }) } catch { $userLanguages = @() }
    [ordered]@{
        caption          = $os.Caption
        version          = $os.Version
        build            = $build
        ubr              = Get-RegistryValue $cvPath 'UBR'
        displayVersion   = $displayVersion
        architecture     = $os.OSArchitecture
        is64Bit          = [Environment]::Is64BitOperatingSystem
        supported        = ([Environment]::Is64BitOperatingSystem -and $build -ge 19045)
        uiLanguage       = (Get-UICulture).Name
        userLanguages    = $userLanguages
        lastBootUpTime   = $os.LastBootUpTime.ToString('s')
    }
}
Write-Section 'Windows'
Write-Item 'Edition' $windows.caption
Write-Item 'Version' ('{0} (build {1}.{2})' -f $windows.displayVersion, $windows.build, $windows.ubr)
Write-Item 'Architecture' $windows.architecture
Write-Item 'Display language' $windows.uiLanguage
Write-Item 'Keyboard languages' (@($windows.userLanguages) -join ', ')
if ($windows.supported -eq $false) {
    Write-Host '  Basira needs 64-bit Windows 10 22H2 (build 19045) or Windows 11.' -ForegroundColor Yellow
}

# --- .NET ---------------------------------------------------------------------
$dotnet = Invoke-Collector 'dotnet' {
    $ErrorActionPreference = 'Continue'
    $runtimes = @()
    $command = Get-Command -Name dotnet -CommandType Application -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($command) {
        $runtimes = @(& $command.Path --list-runtimes 2>$null)
    }
    if ($runtimes.Count -eq 0) {
        $shared = Join-Path $env:ProgramFiles 'dotnet\shared'
        if (Test-Path -LiteralPath $shared) {
            foreach ($framework in @(Get-ChildItem -LiteralPath $shared -Directory)) {
                foreach ($version in @(Get-ChildItem -LiteralPath $framework.FullName -Directory)) {
                    $runtimes += ('{0} {1}' -f $framework.Name, $version.Name)
                }
            }
        }
    }
    $release = Get-RegistryValue 'HKLM:\SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full' 'Release'
    [ordered]@{
        runtimes                 = $runtimes
        netFramework4Release     = $release
    }
}
Write-Section '.NET'
if (@($dotnet.runtimes).Count -eq 0) { Write-Item 'Runtimes' 'none (Basira ships its own runtime)' }
foreach ($r in @($dotnet.runtimes)) { Write-Host ('  ' + $r) }

# --- Audio input --------------------------------------------------------------
$audio = Invoke-Collector 'audio' {
    # Capture endpoints have IDs that start with {0.0.1.00000000}; playback ones with {0.0.0.00000000}.
    $mics = @(Get-CimInstance -ClassName Win32_PnPEntity -Filter "PNPClass = 'AudioEndpoint'" |
        Where-Object { $_.PNPDeviceID -like 'SWD\MMDEVAPI\{0.0.1.00000000}*' } |
        ForEach-Object { [ordered]@{ name = $_.Name; status = $_.Status } })
    $consent = 'Software\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\microphone'
    [ordered]@{
        inputDevices = $mics
        privacy      = [ordered]@{
            deviceAccess   = Get-RegistryValue ('HKLM:\' + $consent) 'Value'
            appsAccess     = Get-RegistryValue ('HKCU:\' + $consent) 'Value'
            desktopApps    = Get-RegistryValue ('HKCU:\' + $consent + '\NonPackaged') 'Value'
        }
    }
}
Write-Section 'Microphones'
if (@($audio.inputDevices).Count -eq 0) { Write-Host '  No microphone found.' -ForegroundColor Yellow }
foreach ($m in @($audio.inputDevices)) { Write-Item $m.name $m.status }
Write-Item 'Privacy: device access' $audio.privacy.deviceAccess
Write-Item 'Privacy: apps' $audio.privacy.appsAccess
Write-Item 'Privacy: desktop apps' $audio.privacy.desktopApps

# --- Keyboard -----------------------------------------------------------------
$keyboard = Invoke-Collector 'keyboard' {
    $names = @(Get-CimInstance -ClassName Win32_Keyboard | ForEach-Object { $_.Description } | Select-Object -Unique)
    $answer = $RightCtrl
    if (-not $answer) {
        if ($NonInteractive -or -not [Environment]::UserInteractive) {
            $answer = 'unknown'
        }
        else {
            Write-Host ''
            Write-Host 'Look at the keyboard: is there a Ctrl key on the RIGHT side of the space bar?' -ForegroundColor Cyan
            Write-Host '(Some laptops have a Copilot or menu key there instead.)'
            $reply = Read-Host 'Right Ctrl present? [y/n, Enter = not sure]'
            switch -Regex ($reply) {
                '^\s*[yY]' { $answer = 'yes' }
                '^\s*[nN]' { $answer = 'no' }
                default { $answer = 'unknown' }
            }
        }
    }
    [ordered]@{
        devices          = $names
        rightCtrlPresent = $answer
    }
}
Write-Section 'Keyboard'
Write-Item 'Right Ctrl key' $keyboard.rightCtrlPresent
foreach ($k in @($keyboard.devices)) { Write-Host ('  ' + $k) }

# --- NVDA ---------------------------------------------------------------------
$nvda = Invoke-Collector 'nvda' {
    $key = Get-ItemProperty -Path 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\NVDA' -ErrorAction SilentlyContinue
    if (-not $key) {
        $key = Get-ItemProperty -Path 'HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\NVDA' -ErrorAction SilentlyContinue
    }
    $addons = @()
    $addonDir = Join-Path $env:APPDATA 'nvda\addons'
    if (Test-Path -LiteralPath $addonDir) {
        $addons = @(Get-ChildItem -LiteralPath $addonDir -Directory | ForEach-Object { $_.Name })
    }
    $version = $null
    $installDir = $null
    if ($key) {
        $version = [string]$key.DisplayVersion
        $installDir = [string]$key.UninstallDirectory
    }
    [ordered]@{
        installed  = [bool]$key
        version    = $version
        installDir = $installDir
        running    = [bool](Get-Process -Name nvda -ErrorAction SilentlyContinue)
        addons     = $addons
    }
}
Write-Section 'NVDA'
if ($nvda.installed) {
    Write-Item 'Version' $nvda.version
    Write-Item 'Running' $nvda.running
    Write-Item 'Add-ons' (@($nvda.addons) -join ', ')
}
else {
    Write-Item 'Installed' 'no'
}

# --- Windows 10 ESU -----------------------------------------------------------
$esu = Invoke-Collector 'esu' {
    if ($windows.build -ge 22000) {
        return [ordered]@{ status = 'not needed (Windows 11)'; detail = $null }
    }
    $status = 'unknown'
    $detail = $null
    try {
        # Commercial ESU licences appear as licensing products named like "Client-ESU-Year1".
        # Consumer enrolment (Settings > Windows Update) may not show here, so "unknown" is common.
        $products = @(Get-CimInstance -ClassName SoftwareLicensingProduct -Filter "Name LIKE '%ESU%' AND PartialProductKey IS NOT NULL" -ErrorAction Stop)
        $licensed = @($products | Where-Object { $_.LicenseStatus -eq 1 })
        if ($licensed.Count -gt 0) {
            $status = 'licensed'
            $detail = (@($licensed | ForEach-Object { $_.Name }) -join '; ')
        }
        elseif ($products.Count -gt 0) {
            $status = 'ESU product present but not activated'
            $detail = (@($products | ForEach-Object { $_.Name }) -join '; ')
        }
    }
    catch {
        $detail = $_.Exception.Message
    }
    [ordered]@{ status = $status; detail = $detail }
}
Write-Section 'Windows 10 Extended Security Updates (hint)'
Write-Item 'ESU' $esu.status

# --- Arabic speech and OCR ----------------------------------------------------
$arabic = Invoke-Collector 'arabic' {
    $capabilities = 'requires administrator'
    if ($isAdmin) {
        $capabilities = @(Get-WindowsCapability -Online |
            Where-Object { $_.Name -like 'Language.*~~~ar-*' -or $_.Name -like 'Language.TextToSpeech~~~en-*' } |
            ForEach-Object { [ordered]@{ name = $_.Name; state = [string]$_.State } })
    }
    $voices = @()
    $tokens = 'HKLM:\SOFTWARE\Microsoft\Speech_OneCore\Voices\Tokens'
    if (Test-Path -LiteralPath $tokens) {
        foreach ($token in @(Get-ChildItem -LiteralPath $tokens)) {
            $name = (Get-ItemProperty -LiteralPath $token.PSPath -ErrorAction SilentlyContinue).'(default)'
            if ($name) { $voices += $name }
        }
    }
    $ocr = 'unknown'
    try {
        $null = [Windows.Media.Ocr.OcrEngine, Windows.Foundation, ContentType = WindowsRuntime]
        $ocr = @([Windows.Media.Ocr.OcrEngine]::AvailableRecognizerLanguages | ForEach-Object { $_.LanguageTag })
    }
    catch {
        $ocr = 'unknown'
    }
    [ordered]@{
        capabilities = $capabilities
        oneCoreVoices = $voices
        ocrLanguages = $ocr
    }
}
Write-Section 'Arabic speech and OCR'
Write-Item 'OneCore voices' (@($arabic.oneCoreVoices) -join '; ')
Write-Item 'OCR languages' (@($arabic.ocrLanguages) -join ', ')
if ($arabic.capabilities -is [string]) {
    Write-Item 'Capabilities' $arabic.capabilities
}
else {
    foreach ($c in @($arabic.capabilities)) { Write-Item $c.name $c.state }
}

# --- Optional network test ----------------------------------------------------
$network = [ordered]@{ tested = $false }
if ($UplinkTest) {
    $network = Invoke-Collector 'network' {
        [Net.ServicePointManager]::SecurityProtocol = [Net.ServicePointManager]::SecurityProtocol -bor [Net.SecurityProtocolType]::Tls12
        $oldProgress = $ProgressPreference
        $ProgressPreference = 'SilentlyContinue'
        try {
            $downBytes = 5000000
            $watch = [Diagnostics.Stopwatch]::StartNew()
            $null = Invoke-WebRequest -Uri ('https://speed.cloudflare.com/__down?bytes={0}' -f $downBytes) -UseBasicParsing -TimeoutSec 120
            $watch.Stop()
            $downMbps = [math]::Round($downBytes * 8 / 1e6 / $watch.Elapsed.TotalSeconds, 1)

            $upBytes = 2000000
            $body = New-Object byte[] $upBytes
            $watch = [Diagnostics.Stopwatch]::StartNew()
            $null = Invoke-WebRequest -Uri 'https://speed.cloudflare.com/__up' -Method Post -Body $body -ContentType 'application/octet-stream' -UseBasicParsing -TimeoutSec 120
            $watch.Stop()
            $upMbps = [math]::Round($upBytes * 8 / 1e6 / $watch.Elapsed.TotalSeconds, 1)

            $latency = [ordered]@{}
            foreach ($url in @('https://api.anthropic.com/', 'https://generativelanguage.googleapis.com/')) {
                $watch = [Diagnostics.Stopwatch]::StartNew()
                try { $null = Invoke-WebRequest -Uri $url -Method Head -UseBasicParsing -TimeoutSec 15 } catch { }
                $watch.Stop()
                $latency[$url] = [int]$watch.Elapsed.TotalMilliseconds
            }
            [ordered]@{ tested = $true; downloadMbps = $downMbps; uploadMbps = $upMbps; firstResponseMs = $latency }
        }
        finally {
            $ProgressPreference = $oldProgress
        }
    }
    Write-Section 'Network'
    Write-Item 'Download (Mbit/s)' $network.downloadMbps
    Write-Item 'Upload (Mbit/s)' $network.uploadMbps
    if ($network.firstResponseMs) {
        foreach ($k in $network.firstResponseMs.Keys) { Write-Item $k ('{0} ms' -f $network.firstResponseMs[$k]) }
    }
}

# --- Assessment ---------------------------------------------------------------
$notes = @()
$weak = $false
if ($memory.usableGB -and $memory.usableGB -le 4.5) { $weak = $true; $notes += 'RAM is 4 GB or less: this PC matches the weak target profile.' }
if ($cpu.threads -and $cpu.threads -le 2) { $weak = $true; $notes += 'Two threads or fewer: this PC matches the weak target profile.' }
foreach ($d in @($disks.physical)) {
    if ([string]$d.mediaType -like 'HDD*') { $weak = $true; $notes += ('Spinning hard disk ({0}): expect slow cold starts.' -f $d.model) }
}
if (-not $weak -and $memory.usableGB -gt 8) {
    $notes += 'This PC is much faster than the target. To emulate a weak PC, run msconfig > Boot > Advanced options: Maximum memory 4096 MB, Number of processors 2.'
}
if (-not $nvda.installed) { $notes += 'NVDA is not installed yet: run scripts\setup-father-pc.ps1.' }
if (@($audio.inputDevices).Count -eq 0) { $notes += 'No microphone was found.' }
if ($audio.privacy.desktopApps -eq 'Deny' -or $audio.privacy.appsAccess -eq 'Deny' -or $audio.privacy.deviceAccess -eq 'Deny') {
    $notes += 'Microphone access is turned off in Settings > Privacy > Microphone. Turn on access for desktop apps.'
}
if ($keyboard.rightCtrlPresent -eq 'no') { $notes += 'No Right Ctrl key: choose another talk key during Basira setup (for example the Application key).' }

Write-Section 'Notes'
if ($notes.Count -eq 0) { Write-Host '  Nothing to report.' }
foreach ($n in $notes) { Write-Host ('  - ' + $n) }

$inventory = [ordered]@{
    schema        = 'basira-inventory/1'
    scriptVersion = $ScriptVersion
    label         = $Label
    generatedUtc  = (Get-Date).ToUniversalTime().ToString('yyyy-MM-ddTHH:mm:ssZ')
    elevated      = $isAdmin
    computer      = $computer
    cpu           = $cpu
    memory        = $memory
    disks         = $disks
    windows       = $windows
    dotnet        = $dotnet
    audio         = $audio
    keyboard      = $keyboard
    nvda          = $nvda
    esu           = $esu
    arabic        = $arabic
    network       = $network
    weakProfile   = $weak
    notes         = $notes
}

try {
    $json = $inventory | ConvertTo-Json -Depth 8
    $directory = Split-Path -Parent $OutFile
    if ($directory -and -not (Test-Path -LiteralPath $directory)) {
        New-Item -ItemType Directory -Path $directory -Force | Out-Null
    }
    [IO.File]::WriteAllText($OutFile, $json, (New-Object Text.UTF8Encoding($false)))
    Write-Host ''
    Write-Host ('Saved ' + $OutFile) -ForegroundColor Green
    exit 0
}
catch {
    Write-Host ('Could not write {0}: {1}' -f $OutFile, $_.Exception.Message) -ForegroundColor Red
    exit 1
}
