[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [string]$NvdaInstaller,
    [ValidatePattern('^(Windows|[a-z]{2,3}(_[A-Z]{2})?)$')]
    [string]$NvdaLanguage = 'ar',
    [ValidateSet('desktop', 'laptop', 'auto')]
    [string]$KeyboardLayout = 'auto',
    [switch]$AutoSayAllOnPageLoad,
    [string]$BridgeAddon,
    [string]$MubsirExe,
    [string]$User,
    [switch]$SkipNvda,
    [switch]$SkipCapabilities,
    [switch]$SkipTask,
    [switch]$DryRun
)

$ErrorActionPreference = 'Stop'
if ($DryRun) { $WhatIfPreference = $true }

$BundledApp = Join-Path $PSScriptRoot 'Mubsir'
if (-not $BridgeAddon) {
    $found = Get-ChildItem -LiteralPath $PSScriptRoot -Filter '*.nvda-addon' -File -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($found) { $BridgeAddon = $found.FullName }
}

$NvdaVersion = '2026.2'
$NvdaVersionPattern = '^2026\.2(\.|$)'
$NvdaUrl = 'https://download.nvaccess.org/releases/2026.2/nvda_2026.2.exe'
$NvdaSha256 = 'F3F8D29974A88D687B3C4809BE192219EC579C5BDABCDA5AAF53635288BCA824'
$NvdaSignerPattern = '(^|,\s*)CN=NV Access Limited(,|$)'
$NvdaSchemaVersion = 24
$NvdaEaseOfAccessName = 'nvda_nvda_v1'
$RequiredCapabilities = @(
    'Language.Basic~~~ar-SA~0.0.1.0',
    'Language.TextToSpeech~~~ar-SA~0.0.1.0',
    'Language.OCR~~~ar-SA~0.0.1.0',
    'Language.TextToSpeech~~~en-US~0.0.1.0'
)
$OptionalCapabilities = @('Language.TextToSpeech~~~ar-EG~0.0.1.0')
$TaskName = 'Mubsir'

$script:Results = New-Object System.Collections.Generic.List[object]
$script:Manual = New-Object System.Collections.Generic.List[string]

function Write-Step([string]$Text) { Write-Host ''; Write-Host ('==> ' + $Text) -ForegroundColor Cyan }
function Write-Ok([string]$Text) { Write-Host ('    OK   ' + $Text) -ForegroundColor Green }
function Write-Note([string]$Text) { Write-Host ('    NOTE ' + $Text) -ForegroundColor Yellow }
function Write-Bad([string]$Text) { Write-Host ('    FAIL ' + $Text) -ForegroundColor Red }

function Add-Result([string]$Step, [string]$Status, [string]$Detail = '') {
    $script:Results.Add([pscustomobject]@{ Step = $Step; Status = $Status; Detail = $Detail })
}

function Stop-Setup([string]$Message) {
    Write-Host ''
    Write-Host $Message -ForegroundColor Red
    exit 2
}

function Invoke-Change([string]$ChangeTarget, [string]$ChangeDescription, [scriptblock]$ChangeScript) {
    if ($PSCmdlet.ShouldProcess($ChangeTarget, $ChangeDescription)) {
        & $ChangeScript
        return $true
    }
    return $false
}

function Test-IsAdmin {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    return (New-Object Security.Principal.WindowsPrincipal($identity)).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

function Assert-SupportedWindows {
    $os = Get-CimInstance -ClassName Win32_OperatingSystem
    $build = [int]$os.BuildNumber
    Write-Ok ('{0}, build {1}, {2}' -f $os.Caption, $build, $os.OSArchitecture)
    if (-not [Environment]::Is64BitOperatingSystem) {
        Stop-Setup ('This PC runs 32-bit Windows. NVDA 2026 and Mubsir need 64-bit Windows 10 (version 22H2) or Windows 11. ' +
            'A 32-bit Windows cannot be upgraded in place; it needs a fresh 64-bit installation if the processor supports it.')
    }
    if ([Environment]::OSVersion.Version.Major -lt 10 -or $build -lt 19045) {
        Stop-Setup (('This PC runs {0} (build {1}). Mubsir needs Windows 10 version 22H2 (build 19045) or Windows 11. ' -f $os.Caption, $build) +
            'Install the Windows updates (Settings > Update & Security > Windows Update), then run this script again.')
    }
    if (-not [Environment]::Is64BitProcess) {
        Stop-Setup 'This is 32-bit PowerShell. Open "Windows PowerShell" (not the x86 one) as administrator and run the script again.'
    }
}

function Get-TargetUser([string]$Name) {
    if (-not $Name) {
        $Name = (Get-CimInstance -ClassName Win32_ComputerSystem).UserName
        if (-not $Name) {
            $Name = '{0}\{1}' -f $env:USERDOMAIN, $env:USERNAME
            Write-Note ('Nobody is signed in at the console; configuring the current account {0}.' -f $Name)
        }
    }
    try {
        $sid = (New-Object Security.Principal.NTAccount($Name)).Translate([Security.Principal.SecurityIdentifier]).Value
    }
    catch {
        Stop-Setup ('Windows account "{0}" was not found.' -f $Name)
    }
    $profileKey = 'HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion\ProfileList\' + $sid
    $profileDir = (Get-ItemProperty -LiteralPath $profileKey -ErrorAction SilentlyContinue).ProfileImagePath
    $hive = 'Registry::HKEY_USERS\' + $sid
    if (-not $profileDir -or -not (Test-Path -LiteralPath $hive)) {
        Stop-Setup ('{0} is not signed in, so their settings cannot be written. Sign in as {0} and run the script again.' -f $Name)
    }
    $folders = Get-ItemProperty -LiteralPath ($hive + '\Software\Microsoft\Windows\CurrentVersion\Explorer\Shell Folders') -ErrorAction SilentlyContinue
    $roaming = Join-Path $profileDir 'AppData\Roaming'
    $local = Join-Path $profileDir 'AppData\Local'
    if ($folders -and $folders.AppData) { $roaming = $folders.AppData }
    if ($folders -and $folders.'Local AppData') { $local = $folders.'Local AppData' }
    return [pscustomobject]@{
        Name           = $Name
        Sid            = $sid
        ProfileDir     = $profileDir
        RoamingAppData = $roaming
        LocalAppData   = $local
        Hive           = $hive
    }
}

function Get-NvdaInstall {
    $key = Get-ItemProperty -LiteralPath 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\NVDA' -ErrorAction SilentlyContinue
    if (-not $key) { return $null }
    $dir = [string]$key.UninstallDirectory
    if (-not $dir) { $dir = [string]$key.InstallDir }
    if (-not $dir) { return $null }
    return [pscustomobject]@{
        Version = [string]$key.DisplayVersion
        Dir     = $dir
        Exe     = Join-Path $dir 'nvda.exe'
        Slave   = Join-Path $dir 'nvda_slave.exe'
    }
}

function Test-NvdaInstaller([string]$Path) {
    $hash = (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash
    if ($hash -ne $NvdaSha256) {
        throw ('SHA-256 of {0} is {1}, expected {2}. Do not run this file. Download NVDA {3} again from https://www.nvaccess.org/download/' -f $Path, $hash, $NvdaSha256, $NvdaVersion)
    }
    Write-Ok 'SHA-256 matches the pinned value'
    $signature = Get-AuthenticodeSignature -FilePath $Path
    if ($signature.Status -ne 'Valid') {
        throw ('The installer signature is not valid ({0}): {1}' -f $signature.Status, $signature.StatusMessage)
    }
    $subject = [string]$signature.SignerCertificate.Subject
    if ($subject -notmatch $NvdaSignerPattern) {
        throw ('The installer is signed by "{0}", not by NV Access Limited.' -f $subject)
    }
    Write-Ok ('Signed by ' + $subject)
}

function Get-NvdaInstaller {
    if ($NvdaInstaller) {
        if (-not (Test-Path -LiteralPath $NvdaInstaller -PathType Leaf)) { throw ('Installer not found: ' + $NvdaInstaller) }
        return (Resolve-Path -LiteralPath $NvdaInstaller).ProviderPath
    }
    $folder = Join-Path $env:TEMP 'mubsir-setup'
    New-Item -ItemType Directory -Path $folder -Force -WhatIf:$false | Out-Null
    $file = Join-Path $folder ('nvda_{0}.exe' -f $NvdaVersion)
    if (Test-Path -LiteralPath $file) {
        if ((Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash -eq $NvdaSha256) {
            Write-Ok ('Using the installer downloaded earlier: ' + $file)
            return $file
        }
        Remove-Item -LiteralPath $file -Force -WhatIf:$false
    }
    Write-Host ('    Downloading NVDA {0} (about 62 MB) from {1}' -f $NvdaVersion, $NvdaUrl)
    [Net.ServicePointManager]::SecurityProtocol = [Net.ServicePointManager]::SecurityProtocol -bor [Net.SecurityProtocolType]::Tls12
    $partial = $file + '.partial'
    $oldProgress = $ProgressPreference
    $ProgressPreference = 'SilentlyContinue'
    try {
        Invoke-WebRequest -Uri $NvdaUrl -OutFile $partial -UseBasicParsing
    }
    finally {
        $ProgressPreference = $oldProgress
    }
    Move-Item -LiteralPath $partial -Destination $file -Force -WhatIf:$false
    return $file
}

function Wait-Until([scriptblock]$Condition, [int]$Seconds) {
    $deadline = (Get-Date).AddSeconds($Seconds)
    while ((Get-Date) -lt $deadline) {
        if (& $Condition) { return $true }
        Start-Sleep -Milliseconds 500
    }
    return [bool](& $Condition)
}

function Test-NvdaRunning { return [bool](Get-Process -Name nvda -ErrorAction SilentlyContinue) }

function Stop-Nvda($Nvda) {
    if (-not (Test-NvdaRunning)) { return }
    Write-Host '    Closing NVDA so its settings can be changed (speech stops for a moment).'
    if ($Nvda -and (Test-Path -LiteralPath $Nvda.Exe)) {
        Start-Process -FilePath $Nvda.Exe -ArgumentList '-q' -Wait
    }
    if (-not (Wait-Until { -not (Test-NvdaRunning) } 20)) {
        Write-Note 'NVDA did not close by itself; ending it.'
        Get-Process -Name nvda -ErrorAction SilentlyContinue | Stop-Process -Force
        [void](Wait-Until { -not (Test-NvdaRunning) } 10)
    }
}

function Start-AsUser($Target, [string]$Exe, [string]$Arguments) {
    $name = 'MubsirSetup-' + [guid]::NewGuid().ToString('N').Substring(0, 8)
    if ($Arguments) {
        $action = New-ScheduledTaskAction -Execute $Exe -Argument $Arguments
    }
    else {
        $action = New-ScheduledTaskAction -Execute $Exe
    }
    $principal = New-ScheduledTaskPrincipal -UserId $Target.Name -LogonType Interactive -RunLevel Limited
    $settings = New-ScheduledTaskSettingsSet -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries -ExecutionTimeLimit ([TimeSpan]::Zero)
    Register-ScheduledTask -TaskName $name -Action $action -Principal $principal -Settings $settings | Out-Null
    try {
        Start-ScheduledTask -TaskName $name
        Start-Sleep -Seconds 3
    }
    finally {
        Unregister-ScheduledTask -TaskName $name -Confirm:$false
    }
}

function Start-NvdaAsUser($Target, $Nvda) {
    if (Test-NvdaRunning) { return $true }
    Start-AsUser $Target $Nvda.Slave 'launchNVDA -r'
    return (Wait-Until { Test-NvdaRunning } 30)
}

function Install-Nvda($Target) {
    $current = Get-NvdaInstall
    if ($current -and $current.Version -match $NvdaVersionPattern) {
        Write-Ok ('NVDA {0} is already installed in {1}' -f $current.Version, $current.Dir)
        Add-Result 'Install NVDA' 'already done' $current.Version
        return
    }
    if ($current -and $current.Version) {
        $installedYear = [int](($current.Version -split '\.')[0])
        $installedMajor = [int](($current.Version -split '\.')[1])
        if ($installedYear -gt 2026 -or ($installedYear -eq 2026 -and $installedMajor -gt 2)) {
            Write-Note ('NVDA {0} is newer than the tested {1}; keeping it. Check that the Mubsir Bridge add-on still works.' -f $current.Version, $NvdaVersion)
            Add-Result 'Install NVDA' 'skipped' ('newer version {0} kept' -f $current.Version)
            return
        }
        Write-Note ('NVDA {0} is installed; it will be updated to {1}.' -f $current.Version, $NvdaVersion)
    }
    if ($WhatIfPreference) {
        [void]$PSCmdlet.ShouldProcess($NvdaUrl, ('Download, verify (SHA-256 and signature) and install NVDA {0}' -f $NvdaVersion))
        Add-Result 'Install NVDA' 'dry run'
        return
    }
    $installer = Get-NvdaInstaller
    Test-NvdaInstaller $installer
    Stop-Nvda $current
    Write-Host '    Installing NVDA silently (about a minute)...'
    $process = Start-Process -FilePath $installer -ArgumentList @('--install-silent', '--enable-start-on-logon=True') -Wait -PassThru
    if ($process.ExitCode -ne 0) {
        throw ('The NVDA installer exited with code {0}.' -f $process.ExitCode)
    }
    if (-not (Wait-Until { $n = Get-NvdaInstall; $n -and $n.Version -match $NvdaVersionPattern -and (Test-Path -LiteralPath $n.Exe) } 300)) {
        throw 'NVDA did not appear as installed within 5 minutes.'
    }
    $installed = Get-NvdaInstall
    Write-Ok ('NVDA {0} installed in {1}, with speech on the sign-in screen' -f $installed.Version, $installed.Dir)
    Add-Result 'Install NVDA' 'done' $installed.Version
}

function Set-IniValues([string]$Text, $Settings) {
    $lines = New-Object System.Collections.Generic.List[string]
    if ($Text) { foreach ($line in ($Text -split "`r?`n")) { $lines.Add($line) } }
    while ($lines.Count -gt 0 -and $lines[$lines.Count - 1] -eq '') { $lines.RemoveAt($lines.Count - 1) }

    foreach ($section in $Settings.Keys) {
        if ($section -eq '') {
            $start = 0
        }
        else {
            $header = -1
            for ($i = 0; $i -lt $lines.Count; $i++) {
                if ($lines[$i] -match ('^\s*\[\s*' + [regex]::Escape($section) + '\s*\]\s*$')) { $header = $i; break }
            }
            if ($header -lt 0) {
                $lines.Add('[' + $section + ']')
                $header = $lines.Count - 1
            }
            $start = $header + 1
        }
        $end = $start
        while ($end -lt $lines.Count -and $lines[$end] -notmatch '^\s*\[') { $end++ }

        $pairs = $Settings[$section]
        foreach ($key in $pairs.Keys) {
            $value = $pairs[$key]
            if ($value -is [bool]) { if ($value) { $value = 'True' } else { $value = 'False' } }
            $indent = ''
            if ($section -ne '') { $indent = "`t" }
            $pattern = '^(\s*)' + [regex]::Escape($key) + '\s*='
            $found = -1
            for ($i = $start; $i -lt $end; $i++) {
                if ($lines[$i] -match $pattern) {
                    if ($found -lt 0) {
                        $found = $i
                        $lines[$i] = $Matches[1] + $key + ' = ' + $value
                    }
                    else {
                        $lines.RemoveAt($i)
                        $i--
                        $end--
                    }
                }
            }
            if ($found -lt 0) {
                $lines.Insert($end, $indent + $key + ' = ' + $value)
                $end++
            }
        }
    }
    return (($lines -join "`r`n") + "`r`n")
}

function Get-NvdaConfigDir($Target) {
    $inLocal = Get-ItemProperty -LiteralPath 'HKLM:\SOFTWARE\NVDA' -Name configInLocalAppData -ErrorAction SilentlyContinue
    if ($inLocal -and [int]$inLocal.configInLocalAppData -eq 1) {
        return (Join-Path $Target.LocalAppData 'nvda')
    }
    return (Join-Path $Target.RoamingAppData 'nvda')
}

function Set-NvdaConfig($Target) {
    $nvda = Get-NvdaInstall
    if (-not $nvda) {
        if ($WhatIfPreference) {
            Add-Result 'NVDA settings' 'dry run' 'NVDA not installed yet'
            return
        }
        throw 'NVDA is not installed, so its settings cannot be written.'
    }
    $layout = $KeyboardLayout
    if ($layout -eq 'auto') {
        $layout = 'desktop'
        if (Get-CimInstance -ClassName Win32_Battery -ErrorAction SilentlyContinue) { $layout = 'laptop' }
        Write-Ok ('Keyboard layout: {0} (detected; use -KeyboardLayout to change)' -f $layout)
    }
    $settings = [ordered]@{
        'general'        = [ordered]@{ language = $NvdaLanguage; showWelcomeDialogAtStartup = $false }
        'speech'         = [ordered]@{ synth = 'oneCore'; autoLanguageSwitching = $true }
        'keyboard'       = [ordered]@{ keyboardLayout = $layout }
        'virtualBuffers' = [ordered]@{ autoSayAllOnPageLoad = [bool]$AutoSayAllOnPageLoad }
        'update'         = [ordered]@{ autoCheck = $false; startupNotification = $false; allowUsageStats = $false; askedAllowUsageStats = $true }
    }

    $configDir = Get-NvdaConfigDir $Target
    $iniPath = Join-Path $configDir 'nvda.ini'
    $old = ''
    if (Test-Path -LiteralPath $iniPath) { $old = [IO.File]::ReadAllText($iniPath) }
    $topLevel = [ordered]@{}
    if ($old -notmatch '(?m)^\s*schemaVersion\s*=') { $topLevel['schemaVersion'] = $NvdaSchemaVersion }
    $all = [ordered]@{ '' = $topLevel }
    foreach ($k in $settings.Keys) { $all[$k] = $settings[$k] }
    $new = Set-IniValues $old $all

    if ($new -ceq $old) {
        Write-Ok ('NVDA settings already set in ' + $iniPath)
        Add-Result 'NVDA settings' 'already done' $iniPath
    }
    else {
        $changed = Invoke-Change $iniPath 'Write NVDA settings' {
            Stop-Nvda $nvda
            New-Item -ItemType Directory -Path $configDir -Force | Out-Null
            $backup = $iniPath + '.before-mubsir'
            if ($old -and -not (Test-Path -LiteralPath $backup)) {
                [IO.File]::WriteAllText($backup, $old, (New-Object Text.UTF8Encoding($false)))
                Write-Ok ('Previous settings saved as ' + $backup)
            }
            [IO.File]::WriteAllText($iniPath, $new, (New-Object Text.UTF8Encoding($false)))
        }
        if ($changed) {
            Write-Ok ('NVDA settings written to ' + $iniPath)
            Add-Result 'NVDA settings' 'done' $iniPath
        }
        else {
            Add-Result 'NVDA settings' 'dry run' $iniPath
        }
    }

    $systemConfig = Join-Path $nvda.Dir 'systemConfig'
    $systemIni = Join-Path $systemConfig 'nvda.ini'
    if ((Test-Path -LiteralPath $systemIni) -and [IO.File]::ReadAllText($systemIni) -ceq $new) {
        Write-Ok 'Sign-in screen settings already match'
        Add-Result 'Sign-in screen settings' 'already done' $systemIni
    }
    elseif (Invoke-Change $systemIni 'Copy nvda.ini (without add-ons) for the sign-in screen' {
            New-Item -ItemType Directory -Path $systemConfig -Force | Out-Null
            [IO.File]::WriteAllText($systemIni, $new, (New-Object Text.UTF8Encoding($false)))
        }) {
        Write-Ok ('Sign-in screen settings written to ' + $systemIni)
        Add-Result 'Sign-in screen settings' 'done' $systemIni
    }
    else {
        Add-Result 'Sign-in screen settings' 'dry run' $systemIni
    }

    $eoaKey = $Target.Hive + '\Software\Microsoft\Windows NT\CurrentVersion\Accessibility'
    $current = ''
    $item = Get-ItemProperty -LiteralPath $eoaKey -Name Configuration -ErrorAction SilentlyContinue
    if ($item) { $current = [string]$item.Configuration }
    $names = @($current -split ',' | Where-Object { $_ -ne '' })
    if ($names -contains $NvdaEaseOfAccessName) {
        Write-Ok 'NVDA already starts after sign-in'
        Add-Result 'Start NVDA after sign-in' 'already done'
    }
    elseif (Invoke-Change $eoaKey 'Start NVDA after this user signs in' {
            if (-not (Test-Path -LiteralPath $eoaKey)) { New-Item -Path $eoaKey | Out-Null }
            New-ItemProperty -LiteralPath $eoaKey -Name Configuration -Value ((@($names) + $NvdaEaseOfAccessName) -join ',') -PropertyType String -Force | Out-Null
        }) {
        Write-Ok 'NVDA will start after sign-in'
        Add-Result 'Start NVDA after sign-in' 'done'
    }
    else {
        Add-Result 'Start NVDA after sign-in' 'dry run'
    }
}

function Add-ArabicCapabilities {
    Write-Host '    Reading the list of Windows features (can take a minute)...'
    $all = @(Get-WindowsCapability -Online)
    $byName = @{}
    foreach ($c in $all) { $byName[$c.Name] = $c }
    $wanted = @($RequiredCapabilities)
    foreach ($optional in $OptionalCapabilities) {
        if ($byName.ContainsKey($optional)) { $wanted += $optional }
        else { Write-Note ($optional + ' is not offered by this Windows; skipped.') }
    }
    $restart = $false
    foreach ($name in $wanted) {
        $capability = $byName[$name]
        if (-not $capability) {
            Write-Bad ($name + ' is not offered by this Windows.')
            Add-Result ('Feature ' + $name) 'failed' 'not offered by this Windows'
            continue
        }
        if ([string]$capability.State -eq 'Installed') {
            Write-Ok ($name + ' already installed')
            Add-Result ('Feature ' + $name) 'already done'
            continue
        }
        try {
            $ran = Invoke-Change $name 'Add Windows feature (downloads from Windows Update, can take several minutes)' {
                Write-Host ('    Adding {0}...' -f $name)
                $result = Add-WindowsCapability -Online -Name $name
                if ($result -and $result.RestartNeeded) { $script:RestartNeeded = $true }
            }
            if ($ran) {
                Write-Ok ($name + ' added')
                Add-Result ('Feature ' + $name) 'done'
            }
            else {
                Add-Result ('Feature ' + $name) 'dry run'
            }
        }
        catch {
            Write-Bad ('{0}: {1}' -f $name, $_.Exception.Message)
            Add-Result ('Feature ' + $name) 'failed' $_.Exception.Message
            $script:Manual.Add('Add the Arabic speech and OCR features by hand: Settings > Time & Language > Language > Arabic (Saudi Arabia) > Options. If this PC uses a company update server (WSUS), ask its administrator.')
        }
    }
}

function Install-BridgeAddon($Target) {
    if (-not (Test-Path -LiteralPath $BridgeAddon -PathType Leaf)) { throw ('Add-on file not found: ' + $BridgeAddon) }
    if ([IO.Path]::GetExtension($BridgeAddon) -ne '.nvda-addon') { throw 'The add-on file must end with .nvda-addon.' }
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $zip = [IO.Compression.ZipFile]::OpenRead((Resolve-Path -LiteralPath $BridgeAddon).ProviderPath)
    try {
        $entry = $zip.GetEntry('manifest.ini')
        if (-not $entry) { throw 'The add-on has no manifest.ini.' }
        $reader = New-Object IO.StreamReader($entry.Open())
        try { $manifest = $reader.ReadToEnd() } finally { $reader.Dispose() }
    }
    finally {
        $zip.Dispose()
    }
    if ($manifest -notmatch '(?m)^\s*name\s*=\s*"?mubsirBridge"?\s*$') { throw 'This is not the Mubsir Bridge add-on.' }
    $nvda = Get-NvdaInstall
    if (-not $nvda) { throw 'NVDA is not installed.' }

    $staging = Join-Path $Target.LocalAppData 'Mubsir\setup'
    $copy = Join-Path $staging 'mubsirBridge.nvda-addon'
    $ran = Invoke-Change $copy 'Ask NVDA to install the Mubsir Bridge add-on' {
        New-Item -ItemType Directory -Path $staging -Force | Out-Null
        Copy-Item -LiteralPath $BridgeAddon -Destination $copy -Force
        if (-not (Start-NvdaAsUser $Target $nvda)) { throw 'NVDA did not start, so the add-on could not be installed.' }
        Start-Sleep -Seconds 5
        Start-AsUser $Target $nvda.Slave ('addons_installAddonPackage "{0}"' -f $copy)
    }
    if ($ran) {
        Write-Ok 'NVDA was asked to install Mubsir Bridge.'
        Write-Host '    On the screen, NVDA now asks to confirm the add-on: choose Install/Yes, then let NVDA restart.' -ForegroundColor Yellow
        Add-Result 'Mubsir Bridge add-on' 'confirm on screen' $copy
        $script:Manual.Add('Confirm the Mubsir Bridge install dialog in NVDA and let NVDA restart (if you missed it, open ' + $copy + ' with Enter while NVDA runs).')
    }
    else {
        Add-Result 'Mubsir Bridge add-on' 'dry run'
    }
}

function Register-MubsirTask($Target) {
    $exe = $MubsirExe
    if (-not $exe) {
        $installDir = Join-Path $Target.LocalAppData 'Mubsir\current'
        $exe = Join-Path $installDir 'Mubsir.exe'
        if (Test-Path -LiteralPath (Join-Path $BundledApp 'Mubsir.exe') -PathType Leaf) {
            Invoke-Change 'Copy Mubsir' ('Copy the app to {0}' -f $installDir) {
                Get-Process -Name 'Mubsir' -ErrorAction SilentlyContinue | Stop-Process -Force
                New-Item -ItemType Directory -Path $installDir -Force | Out-Null
                Copy-Item -Path (Join-Path $BundledApp '*') -Destination $installDir -Recurse -Force
            } | Out-Null
        }
    }
    $exists = Test-Path -LiteralPath $exe -PathType Leaf
    $ran = Invoke-Change $TaskName ('Run "{0} --watch" when {1} signs in' -f $exe, $Target.Name) {
        $action = New-ScheduledTaskAction -Execute $exe -Argument '--watch' -WorkingDirectory (Split-Path -Parent $exe)
        $trigger = New-ScheduledTaskTrigger -AtLogOn -User $Target.Name
        $principal = New-ScheduledTaskPrincipal -UserId $Target.Name -LogonType Interactive -RunLevel Limited
        $settings = New-ScheduledTaskSettingsSet -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries -StartWhenAvailable `
            -ExecutionTimeLimit ([TimeSpan]::Zero) -RestartCount 3 -RestartInterval (New-TimeSpan -Minutes 1) `
            -MultipleInstances IgnoreNew -Priority 4
        Register-ScheduledTask -TaskName $TaskName -Action $action -Trigger $trigger -Principal $principal -Settings $settings `
            -Description 'Starts Mubsir, the AI screen assistant, at sign-in. Mubsir --watch restarts itself after a crash.' -Force | Out-Null
        if ($exists) { Enable-ScheduledTask -TaskName $TaskName | Out-Null }
        else { Disable-ScheduledTask -TaskName $TaskName | Out-Null }
    }
    if (-not $ran) {
        Add-Result 'Mubsir logon task' 'dry run' $exe
    }
    elseif ($exists) {
        Write-Ok ('Task "{0}" starts {1} --watch at sign-in' -f $TaskName, $exe)
        Add-Result 'Mubsir logon task' 'done' $exe
    }
    else {
        Write-Note ('Mubsir is not installed yet ({0} is missing): the task was created DISABLED.' -f $exe)
        Add-Result 'Mubsir logon task' 'created disabled' 'Mubsir not installed yet'
        $script:Manual.Add('After installing Mubsir, run this script again (or: Enable-ScheduledTask -TaskName Mubsir) to enable the logon task.')
    }
}

Write-Host 'Mubsir setup for this PC' -ForegroundColor Green
if ($WhatIfPreference) { Write-Host 'DRY RUN: nothing will be changed.' -ForegroundColor Yellow }

if (-not (Test-IsAdmin)) {
    Stop-Setup 'Run this script as administrator: right-click Windows PowerShell, choose "Run as administrator", then run it again.'
}

Write-Step 'Checking Windows'
Assert-SupportedWindows
$target = Get-TargetUser $User
Write-Ok ('Settings will be written for Windows user {0}' -f $target.Name)
$script:RestartNeeded = $false

if ($SkipNvda) {
    Add-Result 'NVDA' 'skipped' '-SkipNvda'
}
else {
    Write-Step ('Installing NVDA {0}' -f $NvdaVersion)
    try { Install-Nvda $target }
    catch { Write-Bad $_.Exception.Message; Add-Result 'Install NVDA' 'failed' $_.Exception.Message }

    Write-Step 'Configuring NVDA'
    try { Set-NvdaConfig $target }
    catch { Write-Bad $_.Exception.Message; Add-Result 'NVDA settings' 'failed' $_.Exception.Message }
}

if ($SkipCapabilities) {
    Add-Result 'Windows features' 'skipped' '-SkipCapabilities'
}
else {
    Write-Step 'Adding Arabic language, speech and OCR features'
    try { Add-ArabicCapabilities }
    catch { Write-Bad $_.Exception.Message; Add-Result 'Windows features' 'failed' $_.Exception.Message }
}

if ($BridgeAddon) {
    Write-Step 'Installing the Mubsir Bridge NVDA add-on'
    try { Install-BridgeAddon $target }
    catch { Write-Bad $_.Exception.Message; Add-Result 'Mubsir Bridge add-on' 'failed' $_.Exception.Message }
}
else {
    Add-Result 'Mubsir Bridge add-on' 'skipped' 'no -BridgeAddon given'
}

if ($SkipTask) {
    Add-Result 'Mubsir logon task' 'skipped' '-SkipTask'
}
else {
    Write-Step 'Creating the Mubsir logon task'
    try { Register-MubsirTask $target }
    catch { Write-Bad $_.Exception.Message; Add-Result 'Mubsir logon task' 'failed' $_.Exception.Message }
}

if (-not $SkipNvda -and -not $WhatIfPreference) {
    $nvdaNow = Get-NvdaInstall
    if ($nvdaNow -and -not (Test-NvdaRunning)) {
        Write-Step 'Starting NVDA'
        try {
            if (Start-NvdaAsUser $target $nvdaNow) { Write-Ok 'NVDA is running' }
            else { Write-Note 'NVDA did not start; press Ctrl+Alt+N.' }
        }
        catch { Write-Note ('Could not start NVDA: {0}. Press Ctrl+Alt+N.' -f $_.Exception.Message) }
    }
}

Write-Host ''
Write-Host '================ Summary ================' -ForegroundColor Green
$script:Results | Format-Table -AutoSize -Wrap | Out-String -Width 160 | Write-Host

$mic = Get-ItemProperty -LiteralPath ($target.Hive + '\Software\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\microphone\NonPackaged') -Name Value -ErrorAction SilentlyContinue
$micState = 'unknown'
if ($mic) { $micState = [string]$mic.Value }

Write-Host 'Still to do by hand:' -ForegroundColor Cyan
$steps = @()
if ($script:RestartNeeded) { $steps += 'Restart the PC to finish adding the Windows features.' }
$steps += @($script:Manual)
$steps += ('Microphone: Settings > Privacy > Microphone: turn on "Allow apps to access your microphone" and "Allow desktop apps to access your microphone" (desktop apps now: {0}).' -f $micState)
$steps += 'Sign out and in again: NVDA should speak Arabic at the sign-in screen and start after sign-in. Ctrl+Alt+N also starts NVDA.'
$steps += 'Windows 10 only: enrol in Extended Security Updates (Settings > Update & Security > Windows Update > Enroll now).'
$steps += 'Later, when Mubsir is installed: add the API keys with "mubsir --set-key anthropic" and "mubsir --set-key gemini".'
$n = 1
foreach ($s in $steps) { Write-Host ('  {0}. {1}' -f $n, $s); $n++ }

$failed = @($script:Results | Where-Object { $_.Status -eq 'failed' })
if ($failed.Count -gt 0) {
    Write-Host ''
    Write-Host ('{0} step(s) failed. Fix the cause shown above and run the script again; finished steps are skipped.' -f $failed.Count) -ForegroundColor Red
    exit 1
}
Write-Host ''
Write-Host 'Done.' -ForegroundColor Green
exit 0
