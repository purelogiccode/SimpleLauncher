$script:VmHostAddress = '172.31.176.191'
$script:VmUserName = 'vm'
$script:VmPassword = 'vm'
$script:HarnessRoot = $PSScriptRoot
$script:ShotRoot = Join-Path $PSScriptRoot 'shots'
$script:ReportRoot = Join-Path $PSScriptRoot 'reports'
$script:AskScript = Join-Path $PSScriptRoot 'ask_vision.py'
$script:NavScript = Join-Path $PSScriptRoot 'atspi_nav.py'
$script:FixtureScript = Join-Path $PSScriptRoot 'fixture.py'
$script:DefaultModel = 'xiaomi/mimo-v2.6-flash'
$script:VmSession = $null

function Get-VmCredential {
    [pscredential]::new($script:VmUserName, (ConvertTo-SecureString $script:VmPassword -AsPlainText -Force))
}

function Get-VmSession {
    Import-Module Posh-SSH -ErrorAction Stop
    if ($script:VmSession -and (Get-SSHSession -SessionId $script:VmSession.SessionId -ErrorAction SilentlyContinue)) {
        return $script:VmSession
    }
    $script:VmSession = New-SSHSession -ComputerName $script:VmHostAddress -Credential (Get-VmCredential) -AcceptKey -ConnectionTimeout 20
    return $script:VmSession
}

function Close-VmSession {
    if ($script:VmSession) {
        Remove-SSHSession -SessionId $script:VmSession.SessionId -ErrorAction SilentlyContinue | Out-Null
        $script:VmSession = $null
    }
}

function Invoke-VmShell {
    param(
        [Parameter(Mandatory)][string]$Command,
        [int]$TimeoutSec = 180
    )
    $session = Get-VmSession
    try {
        $result = Invoke-SSHCommand -SessionId $session.SessionId -Command $Command -TimeOut $TimeoutSec
    }
    catch {
        Close-VmSession
        $session = Get-VmSession
        $result = Invoke-SSHCommand -SessionId $session.SessionId -Command $Command -TimeOut $TimeoutSec
    }
    [pscustomobject]@{
        Output     = ($result.Output -join "`n")
        ExitStatus = $result.ExitStatus
    }
}

$script:VmEnv = 'export DISPLAY=:0 XAUTHORITY=/home/vm/.Xauthority XDG_RUNTIME_DIR=/run/user/1000 DBUS_SESSION_BUS_ADDRESS=unix:path=/run/user/1000/bus'

function Invoke-VmXdo {
    param(
        [Parameter(Mandatory)][string]$Script,
        [int]$TimeoutSec = 120
    )
    Invoke-VmShell -Command "$script:VmEnv`n$Script" -TimeoutSec $TimeoutSec
}

function Deploy-VmNav {
    Invoke-VmShell -Command 'mkdir -p /home/vm/vision' -TimeoutSec 30 | Out-Null
    Set-SCPItem -ComputerName $script:VmHostAddress -Credential (Get-VmCredential) -Path $script:NavScript `
        -Destination '/home/vm/vision' -AcceptKey -Force -ErrorAction Stop | Out-Null
    Set-SCPItem -ComputerName $script:VmHostAddress -Credential (Get-VmCredential) -Path $script:FixtureScript `
        -Destination '/home/vm/vision' -AcceptKey -Force -ErrorAction Stop | Out-Null
}

function Invoke-VmPython {
    param(
        [Parameter(Mandatory)][string]$Script,
        [int]$TimeoutSec = 180
    )
    $command = "$script:VmEnv`ntimeout -k 5 $TimeoutSec python3 -u - 2>&1 <<'PYEOF'`n$Script`nPYEOF"
    Invoke-VmShell -Command $command -TimeoutSec ($TimeoutSec + 30)
}

function Stop-VmApp {
    $kill = @'
for p in $(pgrep -f "^/home/vm/SimpleLauncher/SimpleLauncher.Avalonia( |$)"); do
  exe=$(readlink /proc/$p/exe 2>/dev/null)
  [ "$exe" = "/home/vm/SimpleLauncher/SimpleLauncher.Avalonia" ] && kill -9 $p
done
sleep 1
'@
    Invoke-VmXdo -Script $kill -TimeoutSec 60 | Out-Null
}

function Test-VmAtspiReady {
    $check = @'
import sys, json
sys.path.insert(0, "/home/vm/vision")
from atspi_nav import Nav
n = Nav()
entries = n.snapshot()
print("SL_READY: " + json.dumps({"nodes": len(entries),
      "frame": any(e.role == "frame" and e.name == "Simple Launcher" for e in entries)}))
'@
    $result = Invoke-VmPython -Script $check -TimeoutSec 60
    $json = $result.Output | Select-String -Pattern '\{"nodes".*\}' | ForEach-Object { $_.Matches[0].Value } | Select-Object -Last 1
    if (-not $json) { return $false }
    ($json | ConvertFrom-Json).frame
}

function Start-VmApp {
    param([string]$Arguments = '', [int]$SettleSeconds = 8)
    $start = @'
setsid /home/vm/SimpleLauncher/SimpleLauncher.Avalonia __ARGS__ >/tmp/simplelauncher.log 2>&1 < /dev/null &
for i in $(seq 1 40); do xdotool search --name "^Simple Launcher$" >/dev/null 2>&1 && break; sleep 0.5; done
'@ -replace '__ARGS__', $Arguments
    Invoke-VmXdo -Script $start -TimeoutSec 90 | Out-Null
    Start-Sleep -Seconds $SettleSeconds
    # The app occasionally needs a while to register with the AT-SPI bus after a
    # rapid restart; wait patiently for the frame, then retry once with a pause.
    for ($attempt = 1; $attempt -le 3; $attempt++) {
        $deadline = (Get-Date).AddSeconds(60)
        while ((Get-Date) -lt $deadline) {
            if (Test-VmAtspiReady) { return }
            Start-Sleep -Seconds 2
        }
        Write-Host "AT-SPI tree not ready (attempt $attempt); restarting the app"
        Stop-VmApp
        Start-Sleep -Seconds 5
        Invoke-VmXdo -Script $start -TimeoutSec 90 | Out-Null
        Start-Sleep -Seconds $SettleSeconds
    }
}

function Set-VmFixture {
    param([Parameter(Mandatory)][ValidateSet('seed', 'seeded', 'empty', 'clean-state', 'dump')][string]$Name)
    $command = if ($Name -eq 'seeded') { 'seed' } else { $Name }
    $result = Invoke-VmShell -Command "python3 /home/vm/vision/fixture.py $command" -TimeoutSec 60
    if ($result.ExitStatus -ne 0) {
        # On a brand-new VM the DB does not exist until the app has run once.
        Write-Warning "fixture.py $command failed (run the app once to create settings.dat): $($result.Output)"
    }
    $result.Output
}

function Restart-VmApp {
    param([int]$SettleSeconds = 8, [string]$Fixture, [string]$Arguments = '')
    Stop-VmApp
    if ($Fixture) { Set-VmFixture -Name $Fixture | Out-Null }
    Start-VmApp -Arguments $Arguments -SettleSeconds $SettleSeconds
}

function Test-VmNavHealth {
    $check = @'
import sys, json
sys.path.insert(0, "/home/vm/vision")
from atspi_nav import Nav
n = Nav()
entries = n.snapshot()
frame = any(e.role == "frame" and e.name == "Simple Launcher" for e in entries)
tops = sorted({e.name for e in entries if e.role == "menu item" and e.name})
ok = frame and "Options" in tops and "About" in tops
print("SL_HEALTH: " + json.dumps({"ok": ok, "nodes": len(entries), "tops": tops}))
'@
    $result = Invoke-VmPython -Script $check -TimeoutSec 60
    $json = $result.Output | Select-String -Pattern '\{"ok".*\}' | ForEach-Object { $_.Matches[0].Value } | Select-Object -Last 1
    if (-not $json) { return $false }
    ($json | ConvertFrom-Json).ok
}

function Save-VmShot {
    param([Parameter(Mandatory)][string]$Name)
    New-Item -ItemType Directory -Path $script:ShotRoot -Force | Out-Null
    $remotePath = "/tmp/$Name.png"
    $shot = Invoke-VmXdo "gnome-screenshot -f $remotePath"
    if ($shot.ExitStatus -ne 0) { throw "screenshot failed: $($shot.Output)" }
    Get-SCPItem -ComputerName $script:VmHostAddress -Credential (Get-VmCredential) -Path $remotePath `
        -Destination $script:ShotRoot -PathType File -AcceptKey -Force -ErrorAction Stop | Out-Null
    Join-Path $script:ShotRoot "$Name.png"
}

function Invoke-VisionCheck {
    param(
        [Parameter(Mandatory)][string]$Image,
        [Parameter(Mandatory)][string]$Prompt,
        [string]$Model = $script:DefaultModel,
        [int]$MaxTokens = 8000
    )
    $env:OPENROUTER_API_KEY = [Environment]::GetEnvironmentVariable('OPENROUTER_API_KEY', 'User')
    if (-not $env:OPENROUTER_API_KEY) { throw 'OPENROUTER_API_KEY is not set in user environment variables' }
    # The vision provider occasionally returns 5xx/timeout errors; retry a couple
    # of times before treating the answer as unusable.
    $text = ''
    for ($attempt = 1; $attempt -le 3; $attempt++) {
        $output = & python $script:AskScript $Image $Prompt --model $Model --max-tokens $MaxTokens 2>&1
        $text = ($output -join "`n")
        if ($text -match '(?im)^\s*\**VERDICT\s*:\s*(PASS|FAIL)') { return $text }
        if ($attempt -lt 3) { Start-Sleep -Seconds 3 }
    }
    $text
}

function Get-VmWindowList {
    (Invoke-VmXdo 'xwininfo -root -tree | grep -E "^\s+0x" | sed -E "s/^ +//" | cut -c1-160' -TimeoutSec 60).Output
}

function Get-SuiteVerdict {
    param([string]$Text)
    if ($Text -match '(?im)^\s*\**VERDICT\s*:\s*(PASS|FAIL)') { $Matches[1].ToUpper() }
    elseif ($Text -match '(?i)\bPASS\b' -and $Text -notmatch '(?i)\bFAIL\b') { 'PASS' }
    elseif ($Text -match '(?i)\bFAIL\b') { 'FAIL' }
    else { 'UNCLEAR' }
}

function Get-AtspiResult {
    param([string]$Output)
    $line = ($Output -split "`n" | Where-Object { $_ -match '^SL_RESULT: ' } | Select-Object -Last 1)
    if (-not $line) { return $null }
    $line.Substring('SL_RESULT: '.Length) | ConvertFrom-Json
}
