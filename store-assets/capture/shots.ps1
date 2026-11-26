<#
Tray Control for WSL Store captures, one listing locale per run (ASCII only; the guest's Windows
PowerShell 5.1 reads a file without a byte order mark as ANSI).

The real desktop around the focused window or the tray flyout, with stock distros (Ubuntu running and
the default, Debian stopped). Run in the guest's desktop session with the windows-build-tools app-test
helpers, after screenshot-desktop.ps1 -Mode Prepare, with Windows already in the locale's display
language. app_test_ui passes no arguments, so run it through app_test_run, which does:
app_test_run '& C:\apptest\shots.ps1 -Locale de-DE' (store-assets/README.md, "Localized captures", has
every step). One run captures one locale, because a Windows display language change needs a sign-in,
which the host does with a cold VM cycle between locales. Saves to
C:\apptest\out\<Locale>\, named as in store-assets/manifest.json; the kit's renderer crops the captures
using their .json sidecars.

The sample data is the distros' own names and versions, which are product names and stay as they are in
every language, so there is no per-locale fixture: the script only sets the app's language. Every
element is found by its automation ID or the tray tooltip's app name, never by translated text. Lengths
are written in logical pixels (the 1920 x 1080 desktop) and multiplied by the display scale, because UI
Automation, the cursor and the window helpers all work in physical pixels.

Not yet run on the VM.
#>
param(
    # The listing locale to capture, such as de-DE: the app's language and the output folder.
    [Parameter(Mandatory)] [string]$Locale,

    # The display scale the guest runs at, in percent. Store captures use 200 (3840 x 2160).
    [ValidateSet(100, 200)] [int]$DisplayScale = 200
)
$ErrorActionPreference = 'Stop'
. C:\apptest\ui-lib.ps1

$outDir = Join-Path $UiOut $Locale
New-Item $outDir -ItemType Directory -Force | Out-Null
$log = Join-Path $outDir 'shots.txt'
Set-Content $log "shots $Locale $(Get-Date -Format o)"

<# Append a line to this locale's log. #>
function Note([string]$Message) {
    Add-Content $log $Message
}

# Physical pixels per logical pixel.
$px = $DisplayScale / 100

<# A logical length or coordinate in physical pixels. #>
function P([double]$Logical) {
    [int]($Logical * $px)
}

# The helpers gained Find-ById for the localized captures; keep the script usable with an older copy.
if (-not (Get-Command Find-ById -ErrorAction SilentlyContinue)) {
    function Find-ById($Element, [string]$AutomationId, [string]$Type = $null) {
        $cond = New-Object System.Windows.Automation.PropertyCondition($UiA::AutomationIdProperty, $AutomationId)
        $Element.FindAll('Descendants', $cond) |
            Where-Object { -not $Type -or $_.Current.ControlType.ProgrammaticName -eq "ControlType.$Type" } |
            Select-Object -First 1
    }
}

# A process that is not DPI aware sees a virtualized 1920 x 1080 screen, and every click and window
# size here would be off by the scale.
$bounds = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds
if ($bounds.Width -ne (P 1920)) {
    throw "The screen reads $($bounds.Width) wide, expected $(P 1920) at $DisplayScale%: set the scale first, or the process is not DPI aware"
}

# ---- The app and its language ------------------------------------------------------------------------

# The dev and Store packages have different identity names; both contain WSLMonitor.
$pkg = Get-AppxPackage '*WSLMonitor*' | Select-Object -First 1
if (-not $pkg) { throw 'Tray Control for WSL is not installed' }
$aumid = "$($pkg.PackageFamilyName)!App"
$dataDir = Join-Path $env:LOCALAPPDATA "Packages\$($pkg.PackageFamilyName)\LocalCache\Roaming\WslMonitor"

<#
Set the app's own language to the locale in its settings.json, keeping any other settings. The app is
not running, so it reads the file at its next start. A new file carries the current schema version
(1, the count of SettingsMigrations.Steps), so the app does not treat it as an old file to upgrade.
#>
function Set-AppLanguage {
    $utf8 = New-Object Text.UTF8Encoding $false
    $file = Join-Path $dataDir 'settings.json'
    if (Test-Path $file) {
        $settings = [IO.File]::ReadAllText($file, $utf8) | ConvertFrom-Json
        $settings | Add-Member -NotePropertyName Language -NotePropertyValue $Locale -Force
    }
    else {
        New-Item $dataDir -ItemType Directory -Force | Out-Null
        $settings = [ordered]@{ SchemaVersion = 1; Language = $Locale }
    }
    [IO.File]::WriteAllText($file, ($settings | ConvertTo-Json -Depth 6), $utf8)
    Note "app language: $Locale"
}

# ---- Windows and input -------------------------------------------------------------------------------

<# The tray flyout: the app window that holds the flyout header, or $null. #>
function Get-Flyout {
    Get-AppWindows WslMonitor |
        Where-Object { $_.Current.BoundingRectangle.X -gt -1000 -and (Find-ById $_ 'FlyoutHeader') } |
        Select-Object -First 1
}

<# The details window: an on-screen app window wider than 500 logical pixels that is not the flyout. #>
function Get-DetailsWindow {
    Get-AppWindows WslMonitor |
        Where-Object {
            $r = $_.Current.BoundingRectangle
            $r.Width -gt (P 500) -and $r.X -gt -1000 -and -not (Find-ById $_ 'FlyoutHeader')
        } |
        Select-Object -First 1
}

<# Click a spot inside an element that holds no control (logical offsets), so no focus rectangle or hover shows. #>
function Clear-Focus($Element, [int]$DX, [int]$DY) {
    $b = $Element.Current.BoundingRectangle
    [UiNative]::SetCursorPos([int]($b.X + (P $DX)), [int]($b.Y + (P $DY))) | Out-Null
    Start-Sleep -Milliseconds 200
    [UiNative]::mouse_event(2, 0, 0, 0, [UIntPtr]::Zero)
    [UiNative]::mouse_event(4, 0, 0, 0, [UIntPtr]::Zero)
    Start-Sleep -Milliseconds 500
}

<# Move the pointer to the top centre, away from anything with a hover state. #>
function Park-Pointer {
    [UiNative]::SetCursorPos((P 960), (P 20)) | Out-Null
    Start-Sleep -Milliseconds 500
}

<# Open the tray flyout by clicking the tray icon; returns the flyout window. #>
function Open-Flyout {
    # The tooltip starts with the app's name, which is not translated.
    $icon = Find-TrayIcon 'Tray Control for WSL'
    if (-not $icon) { throw 'no Tray Control for WSL tray icon' }
    Invoke-Click $icon
    $f = $null
    for ($i = 0; $i -lt 10 -and -not $f; $i++) {
        Start-Sleep -Milliseconds 500
        $f = Get-Flyout
    }
    if (-not $f) { throw 'the flyout did not open' }
    Start-Sleep -Seconds 1
    $f
}

<# Save a capture under this locale's folder, and log the window's frame. #>
function Save-LocaleCapture([string]$Name, $Window) {
    Save-DesktopCapture "$Locale\$Name" $Window | Out-Null
    Note "$Name frame: $((Get-WindowFrame $Window) | ConvertTo-Json -Compress)"
}

# ---- Launch ------------------------------------------------------------------------------------------

Get-Process WslMonitor -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Seconds 2
Set-AppLanguage
Start-Process "shell:AppsFolder\$aumid"
Start-Sleep -Seconds 10   # the first poll, so nothing says "Checking WSL..."

# Promote this build's tray icon now that Windows has registered it.
Get-ChildItem 'HKCU:\Control Panel\NotifyIconSettings' | ForEach-Object {
    $p = Get-ItemProperty $_.PSPath
    if ($p.ExecutablePath -like '*WslMonitor*') { Set-ItemProperty $_.PSPath IsPromoted 1 }
}
Start-Sleep -Seconds 3

# A launch from Start opens the details window; close it, so the tray captures show the app as it
# usually runs, with no taskbar button.
for ($i = 0; $i -lt 5 -and ($open = Get-DetailsWindow); $i++) {
    $open.GetCurrentPattern([System.Windows.Automation.WindowPattern]::Pattern).Close()
    Start-Sleep -Seconds 2
}
Note "details window open before the tray captures: $([bool](Get-DetailsWindow))"

# ---- 00 tray list ------------------------------------------------------------------------------------
$f = Open-Flyout
Clear-Focus $f 20 18
Park-Pointer
Save-LocaleCapture '00-tray-dark' $f

# ---- 01 Stop asks for a second click -----------------------------------------------------------------
# The flyout is still open; the running distro's button is Stop. Click it once (red "Stop?" for 4 s).
$stop = Find-ById $f 'DistroPower_Ubuntu' 'Button'
if (-not $stop) { throw 'no DistroPower_Ubuntu in the flyout: Ubuntu is missing, or the installed build predates the automation IDs' }
$labelBefore = $stop.Current.Name
Invoke-Click $stop
Start-Sleep -Milliseconds 300

# Move the pointer off the button within the flyout, in small real moves, so WinUI sees it leave the
# button and the row (a jump straight off the window leaves their hover state on).
$fb = $f.Current.BoundingRectangle
$sb = $stop.Current.BoundingRectangle
$x0 = [int]($sb.X + $sb.Width / 2)
$y0 = [int]($sb.Y + $sb.Height / 2)
$x1 = [int]($fb.X + $fb.Width / 2)
$y1 = [int]($fb.Y + (P 22))
for ($k = 1; $k -le 12; $k++) {
    # Relative mouse_event moves (MOUSEEVENTF_MOVE): real input, which SetCursorPos is not.
    [UiNative]::mouse_event(1, [int](($x1 - $x0) / 12), [int](($y1 - $y0) / 12), 0, [UIntPtr]::Zero)
    Start-Sleep -Milliseconds 30
}
Start-Sleep -Milliseconds 300
Save-LocaleCapture '01-stop-confirm-dark' $f

# The confirmation relabels the button ("Stop" to "Stop?" in English); log both labels to check it.
$labelAfter = (Find-ById $f 'DistroPower_Ubuntu' 'Button').Current.Name
Note "01 power button: '$labelBefore' then '$labelAfter'"
if ($labelAfter -eq $labelBefore) { Note '01 WARNING: the button did not change; recapture' }
Start-Sleep -Seconds 5   # let the confirmation lapse, so Ubuntu keeps running

# ---- 02 Overview -------------------------------------------------------------------------------------
$detailsButton = Find-ById $f 'DetailsButton' 'Button'
if (-not $detailsButton) { throw 'no DetailsButton: the installed build predates the automation IDs' }
Invoke-Click $detailsButton
$w = $null
for ($i = 0; $i -lt 20 -and -not $w; $i++) {
    Start-Sleep -Seconds 1
    $w = Get-DetailsWindow
}
if (-not $w) { throw 'no details window' }

# Click empty wallpaper so the flyout closes, as it does when the user clicks away.
[UiNative]::SetCursorPos((P 120), (P 120)) | Out-Null
Start-Sleep -Milliseconds 200
[UiNative]::mouse_event(2, 0, 0, 0, [UIntPtr]::Zero)
[UiNative]::mouse_event(4, 0, 0, 0, [UIntPtr]::Zero)
Start-Sleep -Seconds 1
Note "flyout open before the overview capture: $([bool](Get-Flyout))"
Start-Sleep -Seconds 2

Set-WindowCentered $w (P 1100) (P 700)
Start-Sleep -Seconds 1
Clear-Focus $w 700 80   # right of the page title
Park-Pointer
Save-LocaleCapture '02-overview-dark' $w

# ---- 03 a distro's page ------------------------------------------------------------------------------
$item = Find-ById $w 'DistroNavItem_Ubuntu'
if (-not $item) { throw 'no Ubuntu navigation item' }
Invoke-Click $item
Start-Sleep -Seconds 4   # details such as disk size fill in
Clear-Focus $w 700 80
Park-Pointer
Save-LocaleCapture '03-distro-dark' $w

$w.GetCurrentPattern([System.Windows.Automation.WindowPattern]::Pattern).Close()
Note 'done'
