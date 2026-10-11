<#
.SYNOPSIS
    Prepares a Windows test PC for the Basira dev loop (run once as administrator).

.DESCRIPTION
    Safe to run again: every step checks what is already done.
      1. .NET 10 SDK (as required by global.json) and Git, installed with winget when missing.
      2. OpenSSH Server: Windows feature, sshd service (automatic start), firewall rule,
         and Windows PowerShell as the default SSH shell.
      3. Optionally the Mac's public key for password-free SSH (-AuthorizedKey).
      4. Scheduled task "BasiraDev": runs the dev build (<repo>\artifacts\dev\Basira.exe --dev)
         only while the user is signed in, on their desktop. scripts/dev/run.sh starts it, because
         a program started straight from SSH cannot reach the desktop, keyboard hooks or audio.
    It ends with this PC's IP address and the ssh command to use from the Mac.

    Exit codes: 0 done, 1 one or more steps failed, 2 this PC or session cannot be set up.

.PARAMETER RepoPath
    Where the repository is (or will be) cloned on this PC. Default: C:\dev\basira.

.PARAMETER User
    Windows account that runs the dev build (DOMAIN\name). Default: the user signed in at the console.

.PARAMETER AuthorizedKey
    An SSH public key line (for example the contents of ~/.ssh/id_ed25519.pub on the Mac).

.PARAMETER SkipTools
    Do not install or check the .NET SDK and Git.

.PARAMETER DryRun
    Show what would change without changing anything (same as -WhatIf).

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File .\scripts\dev\setup-dev-pc.ps1

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File .\setup-dev-pc.ps1 -AuthorizedKey 'ssh-ed25519 AAAA... omar@mac'
#>
[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [string]$RepoPath = 'C:\dev\basira',
    [string]$User,
    [string]$AuthorizedKey,
    [switch]$SkipTools,
    [switch]$DryRun
)

$ErrorActionPreference = 'Stop'
if ($DryRun) { $WhatIfPreference = $true }

$TaskName = 'BasiraDev'
$DevExeRelative = 'artifacts\dev\Basira.exe'
$DefaultSdk = [version]'10.0.100'
$PowerShellExe = Join-Path $env:SystemRoot 'System32\WindowsPowerShell\v1.0\powershell.exe'
$AdministratorsSid = 'S-1-5-32-544'
$SystemSid = 'S-1-5-18'

$script:Results = New-Object System.Collections.Generic.List[object]

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

# Runs $ChangeScript unless this is a dry run; returns $true when it ran. The script block runs in a
# child scope of this function, so a parameter named like a caller variable ($Target) would hide it.
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

function Update-SessionPath {
    $machine = [Environment]::GetEnvironmentVariable('Path', 'Machine')
    $userPath = [Environment]::GetEnvironmentVariable('Path', 'User')
    $env:Path = (@($machine, $userPath) | Where-Object { $_ }) -join ';'
}

function Get-TargetUser([string]$Name) {
    if (-not $Name) {
        $Name = (Get-CimInstance -ClassName Win32_ComputerSystem).UserName
        if (-not $Name) { $Name = '{0}\{1}' -f $env:USERDOMAIN, $env:USERNAME }
    }
    try {
        $sid = (New-Object Security.Principal.NTAccount($Name)).Translate([Security.Principal.SecurityIdentifier]).Value
    }
    catch {
        Stop-Setup ('Windows account "{0}" was not found.' -f $Name)
    }
    $profileKey = 'HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion\ProfileList\' + $sid
    $profileDir = (Get-ItemProperty -LiteralPath $profileKey -ErrorAction SilentlyContinue).ProfileImagePath
    return [pscustomobject]@{
        Name       = $Name
        ShortName  = ($Name -split '\\')[-1]
        Sid        = $sid
        ProfileDir = $profileDir
    }
}

function Test-IsLocalAdmin($Target) {
    try {
        $members = @(Get-LocalGroupMember -SID $AdministratorsSid -ErrorAction Stop)
        foreach ($m in $members) { if ([string]$m.SID -eq $Target.Sid) { return $true } }
        return $false
    }
    catch {
        return $null
    }
}

# --- Tools -------------------------------------------------------------------------
function Get-RequiredSdk {
    $globalJson = Join-Path $RepoPath 'global.json'
    if (Test-Path -LiteralPath $globalJson) {
        try {
            $wanted = [version]((Get-Content -LiteralPath $globalJson -Raw | ConvertFrom-Json).sdk.version)
            if ($wanted.Major -eq 10) { return $wanted }
        }
        catch {
            Write-Note ('Could not read {0}; requiring any .NET 10 SDK.' -f $globalJson)
        }
    }
    return $DefaultSdk
}

function Get-Sdk10 {
    $dotnet = Get-Command -Name dotnet -CommandType Application -ErrorAction SilentlyContinue | Select-Object -First 1
    if (-not $dotnet) { return @() }
    $versions = @()
    foreach ($line in @(& $dotnet.Path --list-sdks 2>$null)) {
        if ($line -match '^(10\.0\.\d+)\s') { $versions += [version]$Matches[1] }
    }
    return @($versions | Sort-Object -Descending)
}

function Invoke-Winget([string[]]$Arguments) {
    $winget = Get-Command -Name winget -CommandType Application -ErrorAction SilentlyContinue | Select-Object -First 1
    if (-not $winget) {
        throw 'winget is not available. Install "App Installer" from the Microsoft Store, or install the tool by hand.'
    }
    & $winget.Path @Arguments --exact --silent --accept-package-agreements --accept-source-agreements
    if ($LASTEXITCODE -ne 0) { throw ('winget {0} failed with exit code {1}.' -f ($Arguments -join ' '), $LASTEXITCODE) }
    Update-SessionPath
}

function Install-DotNetSdk {
    $required = Get-RequiredSdk
    $sdks = @(Get-Sdk10)
    # global.json uses rollForward latestFeature: any 10.0 SDK at or above the pinned version works.
    $ok = @($sdks | Where-Object { $_.Minor -eq $required.Minor -and $_ -ge $required })
    if ($ok.Count -gt 0) {
        Write-Ok ('.NET SDK {0} (needs {1} or a later 10.0 feature band)' -f $ok[0], $required)
        Add-Result '.NET 10 SDK' 'already done' ([string]$ok[0])
        return
    }
    $verb = 'install'
    if ($sdks.Count -gt 0) {
        $verb = 'upgrade'
        Write-Note ('.NET SDK {0} is older than the {1} that global.json asks for.' -f $sdks[0], $required)
    }
    $ran = Invoke-Change 'Microsoft.DotNet.SDK.10' ('winget ' + $verb) {
        Invoke-Winget @($verb, '--id', 'Microsoft.DotNet.SDK.10')
    }
    if (-not $ran) { Add-Result '.NET 10 SDK' 'dry run'; return }
    $after = @(Get-Sdk10)
    if ($after.Count -eq 0) { throw '.NET 10 SDK still not found after winget. Open a new PowerShell window and run this script again.' }
    Write-Ok ('.NET SDK {0} installed' -f $after[0])
    Add-Result '.NET 10 SDK' 'done' ([string]$after[0])
}

function Install-Git {
    $git = Get-Command -Name git -CommandType Application -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($git) {
        Write-Ok ((& $git.Path --version) -join ' ')
        Add-Result 'Git' 'already done'
        return
    }
    $ran = Invoke-Change 'Git.Git' 'winget install' { Invoke-Winget @('install', '--id', 'Git.Git') }
    if (-not $ran) { Add-Result 'Git' 'dry run'; return }
    if (-not (Get-Command -Name git -CommandType Application -ErrorAction SilentlyContinue)) {
        throw 'Git still not found after winget. Open a new PowerShell window and run this script again.'
    }
    Write-Ok 'Git installed'
    Add-Result 'Git' 'done'
}

# --- OpenSSH Server ----------------------------------------------------------------
function Install-OpenSsh {
    $capability = Get-WindowsCapability -Online | Where-Object { $_.Name -like 'OpenSSH.Server~~~~*' } | Select-Object -First 1
    if (-not $capability) { throw 'This Windows does not offer the OpenSSH Server feature.' }
    if ([string]$capability.State -eq 'Installed') {
        Write-Ok ($capability.Name + ' already installed')
        Add-Result 'OpenSSH Server feature' 'already done'
    }
    elseif (Invoke-Change $capability.Name 'Add Windows feature (downloads from Windows Update)' {
            Add-WindowsCapability -Online -Name $capability.Name | Out-Null
        }) {
        Write-Ok ($capability.Name + ' added')
        Add-Result 'OpenSSH Server feature' 'done'
    }
    else {
        Add-Result 'OpenSSH Server feature' 'dry run'
        return
    }

    $service = Get-Service -Name sshd -ErrorAction SilentlyContinue
    if (-not $service) {
        if ($WhatIfPreference) { Add-Result 'sshd service' 'dry run'; return }
        throw 'The sshd service is missing after adding OpenSSH Server. Restart the PC and run this script again.'
    }
    if ($service.StartType -ne 'Automatic' -or $service.Status -ne 'Running') {
        if (Invoke-Change 'sshd' 'Set to start automatically and start now' {
                Set-Service -Name sshd -StartupType Automatic
                Start-Service -Name sshd
            }) {
            Write-Ok 'sshd runs and starts with Windows'
            Add-Result 'sshd service' 'done'
        }
        else { Add-Result 'sshd service' 'dry run' }
    }
    else {
        Write-Ok 'sshd runs and starts with Windows'
        Add-Result 'sshd service' 'already done'
    }

    # The OpenSSH feature normally creates OpenSSH-Server-In-TCP; Basira-Dev-SSH is this script's fallback.
    $rules = @(Get-NetFirewallRule -Name 'OpenSSH-Server-In-TCP', 'Basira-Dev-SSH' -ErrorAction SilentlyContinue |
            Where-Object { [string]$_.Enabled -eq 'True' })
    if ($rules.Count -gt 0) {
        Write-Ok ('Firewall allows SSH (rule "{0}")' -f $rules[0].DisplayName)
        Add-Result 'Firewall rule for SSH' 'already done'
    }
    elseif (Invoke-Change 'Firewall' 'Allow inbound TCP 22 on private and domain networks' {
            New-NetFirewallRule -Name 'Basira-Dev-SSH' -DisplayName 'OpenSSH Server (Basira dev)' -Enabled True `
                -Direction Inbound -Protocol TCP -LocalPort 22 -Action Allow -Profile Private, Domain | Out-Null
        }) {
        Write-Ok 'Firewall rule added (private and domain networks only)'
        Add-Result 'Firewall rule for SSH' 'done'
    }
    else { Add-Result 'Firewall rule for SSH' 'dry run' }

    $current = (Get-ItemProperty -LiteralPath 'HKLM:\SOFTWARE\OpenSSH' -Name DefaultShell -ErrorAction SilentlyContinue).DefaultShell
    if ($current -eq $PowerShellExe) {
        Write-Ok 'Default SSH shell is Windows PowerShell'
        Add-Result 'Default SSH shell' 'already done'
    }
    elseif (Invoke-Change 'HKLM:\SOFTWARE\OpenSSH\DefaultShell' ('Set to ' + $PowerShellExe) {
            if (-not (Test-Path -LiteralPath 'HKLM:\SOFTWARE\OpenSSH')) { New-Item -Path 'HKLM:\SOFTWARE\OpenSSH' -Force | Out-Null }
            New-ItemProperty -LiteralPath 'HKLM:\SOFTWARE\OpenSSH' -Name DefaultShell -Value $PowerShellExe -PropertyType String -Force | Out-Null
        }) {
        Write-Ok 'Default SSH shell set to Windows PowerShell'
        Add-Result 'Default SSH shell' 'done'
    }
    else { Add-Result 'Default SSH shell' 'dry run' }
}

function Add-AuthorizedKey($Target) {
    $key = $AuthorizedKey.Trim()
    if ($key -notmatch '^(ssh-ed25519|ssh-rsa|ecdsa-sha2-nistp\d+|sk-ssh-ed25519@openssh\.com|sk-ecdsa-sha2-nistp256@openssh\.com) [A-Za-z0-9+/=]+( .*)?$') {
        throw 'The -AuthorizedKey value does not look like an SSH public key line (ssh-ed25519 AAAA... name).'
    }
    $isAdmin = Test-IsLocalAdmin $Target
    $files = @()
    if ($isAdmin -ne $false) {
        # sshd_config sends administrators to this shared file; it must be writable only by Administrators and SYSTEM.
        $files += [pscustomobject]@{ Path = Join-Path $env:ProgramData 'ssh\administrators_authorized_keys'; Admin = $true }
    }
    if ($isAdmin -ne $true -and $Target.ProfileDir) {
        $files += [pscustomobject]@{ Path = Join-Path $Target.ProfileDir '.ssh\authorized_keys'; Admin = $false }
    }
    foreach ($f in $files) {
        $existing = @()
        if (Test-Path -LiteralPath $f.Path) { $existing = @(Get-Content -LiteralPath $f.Path) }
        if ($existing -contains $key) {
            Write-Ok ('Key already in ' + $f.Path)
            Add-Result 'SSH key' 'already done' $f.Path
            continue
        }
        if (Invoke-Change $f.Path 'Add the SSH public key' {
                New-Item -ItemType Directory -Path (Split-Path -Parent $f.Path) -Force | Out-Null
                [IO.File]::WriteAllLines($f.Path, [string[]](@($existing) + $key), (New-Object Text.UTF8Encoding($false)))
                if ($f.Admin) {
                    & icacls.exe $f.Path /inheritance:r /grant ('*{0}:F' -f $AdministratorsSid) /grant ('*{0}:F' -f $SystemSid) | Out-Null
                }
                else {
                    & icacls.exe $f.Path /inheritance:r /grant ('*{0}:F' -f $Target.Sid) /grant ('*{0}:F' -f $SystemSid) /grant ('*{0}:F' -f $AdministratorsSid) | Out-Null
                }
                if ($LASTEXITCODE -ne 0) { throw ('icacls could not set permissions on ' + $f.Path) }
            }) {
            Write-Ok ('Key added to ' + $f.Path)
            Add-Result 'SSH key' 'done' $f.Path
        }
        else { Add-Result 'SSH key' 'dry run' $f.Path }
    }
}

# --- Scheduled task ----------------------------------------------------------------
function Register-DevTask($Target) {
    $exe = Join-Path $RepoPath $DevExeRelative
    $ran = Invoke-Change $TaskName ('Run "{0} --dev" on demand, only while {1} is signed in' -f $exe, $Target.Name) {
        $action = New-ScheduledTaskAction -Execute $exe -Argument '--dev' -WorkingDirectory (Split-Path -Parent $exe)
        # Interactive + Limited: runs on the user's desktop without elevation, only when they are signed in.
        $principal = New-ScheduledTaskPrincipal -UserId $Target.Name -LogonType Interactive -RunLevel Limited
        $settings = New-ScheduledTaskSettingsSet -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries `
            -ExecutionTimeLimit ([TimeSpan]::Zero) -MultipleInstances IgnoreNew -Priority 4
        Register-ScheduledTask -TaskName $TaskName -Action $action -Principal $principal -Settings $settings `
            -Description 'Basira dev build, started by scripts/dev/run.sh over SSH.' -Force | Out-Null
    }
    if ($ran) {
        Write-Ok ('Task "{0}" runs {1} --dev' -f $TaskName, $exe)
        Add-Result 'BasiraDev task' 'done' $exe
        if (-not (Test-Path -LiteralPath $exe)) { Write-Note 'The dev build does not exist yet; scripts/dev/run.sh builds it.' }
    }
    else { Add-Result 'BasiraDev task' 'dry run' $exe }
}

function Get-LanAddresses {
    try {
        return @(Get-NetIPAddress -AddressFamily IPv4 -ErrorAction Stop | Where-Object {
                $_.IPAddress -ne '127.0.0.1' -and $_.IPAddress -notlike '169.254.*' -and $_.AddressState -eq 'Preferred'
            } | ForEach-Object { $_.IPAddress })
    }
    catch {
        return @()
    }
}

# --- Main --------------------------------------------------------------------------
Write-Host 'Basira dev setup for this Windows test PC' -ForegroundColor Green
if ($WhatIfPreference) { Write-Host 'DRY RUN: nothing will be changed.' -ForegroundColor Yellow }
if (-not (Test-IsAdmin)) {
    Stop-Setup 'Run this script as administrator: right-click Windows PowerShell, choose "Run as administrator", then run it again.'
}
if (-not [Environment]::Is64BitProcess) {
    Stop-Setup 'This is 32-bit PowerShell. Open "Windows PowerShell" (not the x86 one) as administrator and run the script again.'
}
$target = Get-TargetUser $User
Write-Ok ('Dev build will run as {0}; repository: {1}' -f $target.Name, $RepoPath)

if ($SkipTools) {
    Add-Result 'Tools' 'skipped' '-SkipTools'
}
else {
    Write-Step 'Checking the .NET 10 SDK'
    try { Install-DotNetSdk } catch { Write-Bad $_.Exception.Message; Add-Result '.NET 10 SDK' 'failed' $_.Exception.Message }
    Write-Step 'Checking Git'
    try { Install-Git } catch { Write-Bad $_.Exception.Message; Add-Result 'Git' 'failed' $_.Exception.Message }
}

Write-Step 'Setting up OpenSSH Server'
try { Install-OpenSsh } catch { Write-Bad $_.Exception.Message; Add-Result 'OpenSSH Server' 'failed' $_.Exception.Message }

if ($AuthorizedKey) {
    Write-Step 'Adding the SSH public key'
    try { Add-AuthorizedKey $target } catch { Write-Bad $_.Exception.Message; Add-Result 'SSH key' 'failed' $_.Exception.Message }
}

Write-Step ('Creating the {0} task' -f $TaskName)
try { Register-DevTask $target } catch { Write-Bad $_.Exception.Message; Add-Result 'BasiraDev task' 'failed' $_.Exception.Message }

Write-Host ''
Write-Host '================ Summary ================' -ForegroundColor Green
$script:Results | Format-Table -AutoSize -Wrap | Out-String -Width 160 | Write-Host

$addresses = @(Get-LanAddresses)
Write-Host 'From the Mac:' -ForegroundColor Cyan
if ($addresses.Count -eq 0) {
    Write-Host '  (no IPv4 address found; check the network connection)'
}
foreach ($ip in $addresses) {
    Write-Host ('  ssh {0}@{1}' -f $target.ShortName, $ip)
}
if ($addresses.Count -gt 0) {
    Write-Host ('  export BASIRA_WINPC={0}@{1}' -f $target.ShortName, $addresses[0])
}
Write-Host '  The SSH password is the Windows account password (a PIN does not work). Add -AuthorizedKey to avoid typing it.'
if (-not (Test-Path -LiteralPath (Join-Path $RepoPath '.git'))) {
    Write-Host ''
    Write-Host 'Clone the repository (Git asks you to sign in to GitHub the first time):' -ForegroundColor Cyan
    Write-Host ('  git clone https://github.com/dusk-futile/basira.git "{0}"' -f $RepoPath)
}
try {
    $public = @(Get-NetConnectionProfile -ErrorAction Stop | Where-Object { [string]$_.NetworkCategory -eq 'Public' })
    if ($public.Count -gt 0) {
        Write-Note 'This network is marked Public, so SSH is blocked. Settings > Network & Internet > Properties > Private.'
    }
}
catch { }

$failed = @($script:Results | Where-Object { $_.Status -eq 'failed' })
if ($failed.Count -gt 0) {
    Write-Host ''
    Write-Host ('{0} step(s) failed. Fix the cause shown above and run the script again.' -f $failed.Count) -ForegroundColor Red
    exit 1
}
Write-Host ''
Write-Host 'Done.' -ForegroundColor Green
exit 0
