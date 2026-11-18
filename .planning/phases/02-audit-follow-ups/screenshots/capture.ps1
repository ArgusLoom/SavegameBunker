# capture.ps1 — take the fixed-name D-07 screenshots of the running SavegameBunker app.
#
# Every run starts from the reset demo state: it calls demo-data.ps1 first, then launches the
# Release build with the demo catalog, so before, after-19-20 and after-21 are directly
# comparable pair by pair. Only the app window's rectangle is saved — never the whole desktop.
#
# Usage:
#   powershell -NoProfile -ExecutionPolicy Bypass -File capture.ps1 -OutDir <dir> `
#              [-Shots 01-main-default,...] [-Root <path>]
#   -OutDir  required; the images land as <OutDir>\<shot-name>.png
#   -Shots   optional; defaults to all fourteen names below, in order
#   -Root    optional; passed through to demo-data.ps1 (default %TEMP%\SavegameBunkerDemo)
#
# Exit code: 0 when some shots are missing (each printed as "<name> MISSING <reason>");
# non-zero only when the app cannot be launched or its window is not found.
#
# Controls are driven through UI Automation: toolbar buttons by AutomationId (= their x:Name
# in MainWindow.xaml), row actions by AutomationProperties.Name ("Back up", "Restore",
# "More actions"), dialogs by their button text ("Back up now", "Cancel", "OK").
# Where UI Automation cannot reach a control, the session's own desktop-control or
# screenshot tooling may stand in — record which shots were taken that way in the SUMMARY.

param(
    [Parameter(Mandatory = $true)][string]$OutDir,
    [string]$Shots = '01-main-default,02-main-other-theme,03-toolbar-alt,04-overflow-menu,05-row-actions-idle,06-busy-bulk,07-statusbar,08-restore-steps,09-empty-state,10-addgame-footer,11-changesavefolder-footer,12-scan-footer,13-restorepicker-footer,14-close-hover',
    [string]$Root = (Join-Path $env:TEMP 'SavegameBunkerDemo')
)

$ErrorActionPreference = 'Stop'

Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes, System.Drawing, System.Windows.Forms

Add-Type -Namespace Win32 -Name Native -MemberDefinition @"
[System.Runtime.InteropServices.DllImport("user32.dll")]
public static extern bool GetWindowRect(System.IntPtr hWnd, out RECT rect);
[System.Runtime.InteropServices.DllImport("user32.dll")]
public static extern bool SetCursorPos(int x, int y);
[System.Runtime.InteropServices.DllImport("user32.dll")]
public static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, System.IntPtr dwExtraInfo);
[System.Runtime.InteropServices.DllImport("user32.dll")]
public static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, System.IntPtr dwExtraInfo);
public struct RECT { public int Left, Top, Right, Bottom; }
"@

$script:ShotList = $Shots -split ',' | ForEach-Object { $_.Trim() } | Where-Object { $_ }
$script:Missing = @()

# ---------------------------------------------------------------------------
# Helpers
# ---------------------------------------------------------------------------

function Invoke-DemoResetWithRoot {
    param([string]$DemoRoot)
    $demoData = Join-Path $PSScriptRoot 'demo-data.ps1'
    & powershell -NoProfile -ExecutionPolicy Bypass -File $demoData -Root $DemoRoot
    if ($LASTEXITCODE -ne 0) { throw "demo-data.ps1 failed with exit code $LASTEXITCODE" }
}

function Find-AppWindow {
    # The main window of the running SavegameBunker process (the shell window; dialogs live
    # inside its ContentDialogHost, so the window stays the same top-level HWND).
    $proc = Get-Process -Name 'SavegameBunker' -ErrorAction SilentlyContinue |
        Where-Object { $_.MainWindowHandle -ne 0 } |
        Select-Object -First 1
    if (-not $proc) { return $null }
    $root = [System.Windows.Automation.AutomationElement]::FromHandle($proc.MainWindowHandle)
    return $root
}

function Find-ElementById {
    param($Root, [string]$Id)
    return $Root.FindFirst([System.Windows.Automation.TreeScope]::Descendants,
        (New-Object System.Windows.Automation.PropertyCondition(
            [System.Windows.Automation.AutomationElement]::AutomationIdProperty, $Id)))
}

function Find-ButtonByName {
    # Find an invoke-able element whose Name equals the given text (dialogs name buttons by
    # their caption; row actions by AutomationProperties.Name).
    param($Root, [string]$Name)
    return $Root.FindFirst([System.Windows.Automation.TreeScope]::Descendants,
        (New-Object System.Windows.Automation.AndCondition(
            (New-Object System.Windows.Automation.PropertyCondition(
                [System.Windows.Automation.AutomationElement]::NameProperty, $Name)),
            (New-Object System.Windows.Automation.PropertyCondition(
                [System.Windows.Automation.AutomationElement]::ControlTypeProperty,
                [System.Windows.Automation.ControlType]::Button)))))
}

function Invoke-Element {
    param($Element)
    if (-not $Element) { return $false }
    $invoke = $null
    try { $invoke = $Element.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern) } catch { }
    if ($invoke) { $invoke.Invoke(); return $true }
    $toggle = $null
    try { $toggle = $Element.GetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern) } catch { }
    if ($toggle) { $toggle.Toggle(); return $true }
    $expand = $null
    try { $expand = $Element.GetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern) } catch { }
    if ($expand) { $expand.Expand(); return $true }
    # Last resort: click the element's center with the mouse.
    $r = $Element.Current.BoundingRectangle
    if ($r -eq [System.Windows.Rect]::Empty) { return $false }
    [Win32.Native]::SetCursorPos([int]($r.Left + $r.Width / 2), [int]($r.Top + $r.Height / 2)) | Out-Null
    [Win32.Native]::mouse_event(2, 0, 0, 0, [System.IntPtr]::Zero)   # left down
    [Win32.Native]::mouse_event(4, 0, 0, 0, [System.IntPtr]::Zero)   # left up
    return $true
}

function Save-WindowShot {
    # Copy only the app window's rectangle from the screen — never the whole desktop.
    param([string]$Path)
    $proc = Get-Process -Name 'SavegameBunker' -ErrorAction SilentlyContinue |
        Where-Object { $_.MainWindowHandle -ne 0 } | Select-Object -First 1
    if (-not $proc) { throw 'SavegameBunker window not found' }
    $rect = New-Object Win32.Native+RECT
    [Win32.Native]::GetWindowRect($proc.MainWindowHandle, [ref]$rect) | Out-Null
    $w = $rect.Right - $rect.Left; $h = $rect.Bottom - $rect.Top
    if ($w -le 0 -or $h -le 0) { throw 'window rectangle is empty' }
    $bmp = New-Object System.Drawing.Bitmap($w, $h)
    $gfx = [System.Drawing.Graphics]::FromImage($bmp)
    try {
        $gfx.CopyFromScreen($rect.Left, $rect.Top, 0, 0, (New-Object System.Drawing.Size($w, $h)))
        $bmp.Save($Path, [System.Drawing.Imaging.ImageFormat]::Png)
    } finally { $gfx.Dispose(); $bmp.Dispose() }
}

function Save-ElementShot {
    # Crop the window bitmap to an element's bounding rectangle (used for 05/07).
    param($Element, [string]$Path)
    $proc = Get-Process -Name 'SavegameBunker' -ErrorAction SilentlyContinue |
        Where-Object { $_.MainWindowHandle -ne 0 } | Select-Object -First 1
    $wrect = New-Object Win32.Native+RECT
    [Win32.Native]::GetWindowRect($proc.MainWindowHandle, [ref]$wrect) | Out-Null
    $er = $Element.Current.BoundingRectangle
    $bmp = New-Object System.Drawing.Bitmap($wrect.Right - $wrect.Left, $wrect.Bottom - $wrect.Top)
    $gfx = [System.Drawing.Graphics]::FromImage($bmp)
    $gfx.CopyFromScreen($wrect.Left, $wrect.Top, 0, 0, $bmp.Size)
    $gfx.Dispose()
    $crop = New-Object System.Drawing.Rectangle(
        [int]($er.Left - $wrect.Left), [int]($er.Top - $wrect.Top), [int]$er.Width, [int]$er.Height)
    $crop.Intersect((New-Object System.Drawing.Rectangle(0, 0, $bmp.Width, $bmp.Height)))
    $out = $bmp.Clone($crop, $bmp.PixelFormat)
    $out.Save($Path, [System.Drawing.Imaging.ImageFormat]::Png)
    $out.Dispose(); $bmp.Dispose()
}

function Start-App {
    param([string]$Catalog)
    $exe = Join-Path $PSScriptRoot '..\..\..\..\src\SavegameBunker\bin\Release\net10.0-windows\SavegameBunker.exe'
    $exe = [System.IO.Path]::GetFullPath($exe)
    if (-not (Test-Path $exe)) { throw "Release build not found: $exe (run dotnet build SavegameBunker.sln -c Release first)" }
    Start-Process -FilePath $exe -ArgumentList ('"' + $Catalog + '"')
    # Wait for the main window to appear.
    for ($i = 0; $i -lt 60; $i++) {
        Start-Sleep -Milliseconds 500
        if (Find-AppWindow) { return $true }
    }
    return $false
}

function Stop-App {
    Get-Process -Name 'SavegameBunker' -ErrorAction SilentlyContinue | Stop-Process -Force
    Start-Sleep -Milliseconds 500
}

function Wait-Seconds { param([double]$S) Start-Sleep -Seconds $S }

function Record-Missing { param([string]$Name, [string]$Reason)
    Write-Host "$Name MISSING $Reason"
    $script:Missing += $Name
}

function Record-Ok { param([string]$Name) Write-Host "$Name ok" }

function Dismiss-StartupPrompt {
    # At startup a catalog with unbacked-up changes shows a confirmation ("... has changes not
    # backed up yet — back up now?"). It is not part of any shot: answer it with the cancel button.
    $win = Find-AppWindow
    if (-not $win) { return }
    $cancel = Find-ButtonByName $win 'Cancel'
    if ($cancel) { [void](Invoke-Element $cancel); Wait-Seconds 1 }
}

function Confirm-Dialog {
    # Click a dialog button by caption; returns $true when found and invoked.
    param([string]$Caption)
    $win = Find-AppWindow
    if (-not $win) { return $false }
    $btn = Find-ButtonByName $win $Caption
    if (-not $btn) { return $false }
    [void](Invoke-Element $btn)
    return $true
}

function Backup-SmallGameRow {
    # Back up "Small Save Game" once through its row Backup button, so later shots have a
    # backup to restore from. Row actions are found by AutomationProperties.Name within the
    # row whose Game cell text is the display name.
    $win = Find-AppWindow
    $rows = $win.FindAll([System.Windows.Automation.TreeScope]::Descendants,
        (New-Object System.Windows.Automation.PropertyCondition(
            [System.Windows.Automation.AutomationElement]::ControlTypeProperty,
            [System.Windows.Automation.ControlType]::Custom)))
    foreach ($row in $rows) {
        if ($row.Current.Name -match 'Small Save Game') {
            $btn = Find-ButtonByName $row 'Back up'
            if ($btn) { [void](Invoke-Element $btn); return $true }
        }
    }
    # Fallback: rows may surface their name differently; search every "Back up" button's row.
    $backs = $win.FindAll([System.Windows.Automation.TreeScope]::Descendants,
        (New-Object System.Windows.Automation.PropertyCondition(
            [System.Windows.Automation.AutomationElement]::NameProperty, 'Back up')))
    if ($backs.Count -gt 1) { [void](Invoke-Element $backs[1]); return $true }  # row order: Large, Small, ...
    if ($backs.Count -eq 1) { [void](Invoke-Element $backs[0]); return $true }
    return $false
}

function Send-AltHeld {
    # Hold Alt so the toolbar's access-key underlines show, take the screenshot, release Alt.
    param([string]$Path)
    [Win32.Native]::keybd_event(0x12, 0, 0, [System.IntPtr]::Zero)      # Alt down
    Start-Sleep -Milliseconds 300
    Save-WindowShot $Path
    [Win32.Native]::keybd_event(0x12, 0, 2, [System.IntPtr]::Zero)      # Alt up (KEYEVENTF_KEYUP)
}

# ---------------------------------------------------------------------------
# Shot implementations. Each runs with the app already up from games.json
# (except 09, which relaunches with empty.json). $win is refreshed inside.
# ---------------------------------------------------------------------------

function Shot-01MainDefault {
    param([string]$Path)
    Save-WindowShot $Path
}

function Shot-02MainOtherTheme {
    param([string]$Path)
    $win = Find-AppWindow
    $btn = Find-ElementById $win 'ThemeToggleButton'
    if (-not $btn -or -not (Invoke-Element $btn)) { throw 'ThemeToggleButton not found' }
    Wait-Seconds 1
    Save-WindowShot $Path
}

function Shot-03ToolbarAlt {
    param([string]$Path)
    Send-AltHeld $Path
}

function Shot-04OverflowMenu {
    param([string]$Path)
    $win = Find-AppWindow
    $more = Find-ButtonByName $win 'More actions'
    if (-not $more) { throw 'a row More actions button not found' }
    [void](Invoke-Element $more)
    Wait-Seconds 1
    Save-WindowShot $Path
    [System.Windows.Forms.SendKeys]::SendWait('{ESC}')
    Wait-Seconds 1
}

function Shot-05RowActionsIdle {
    param([string]$Path)
    $win = Find-AppWindow
    $more = Find-ButtonByName $win 'More actions'
    if (-not $more) { throw 'a row action cell not found' }
    Save-ElementShot $more $Path
}

function Shot-06BusyBulk {
    param([string]$Path)
    $win = Find-AppWindow
    $btn = Find-ElementById $win 'BackupAllChangedButton'
    if (-not $btn -or -not (Invoke-Element $btn)) { throw 'BackupAllChangedButton not found or disabled' }
    # The 1 GiB Large Save Game keeps the bulk run busy long enough to photograph: its row
    # spinner and the window-level progress panel. If the backup finishes before the shutter
    # (fast disk), the shot comes out as the settled after-state and must be retaken slower.
    Wait-Seconds 2
    Save-WindowShot $Path
    # Let the bulk run finish before the next shot.
    Wait-Seconds 30
}

function Shot-07Statusbar {
    param([string]$Path)
    $win = Find-AppWindow
    # Crop the window's bottom strip (the status bar row) from the window shot.
    $proc = Get-Process -Name 'SavegameBunker' | Where-Object { $_.MainWindowHandle -ne 0 } | Select-Object -First 1
    $rect = New-Object Win32.Native+RECT
    [Win32.Native]::GetWindowRect($proc.MainWindowHandle, [ref]$rect) | Out-Null
    $w = $rect.Right - $rect.Left
    $bmp = New-Object System.Drawing.Bitmap($w, $rect.Bottom - $rect.Top)
    $gfx = [System.Drawing.Graphics]::FromImage($bmp)
    $gfx.CopyFromScreen($rect.Left, $rect.Top, 0, 0, $bmp.Size)
    $gfx.Dispose()
    $strip = New-Object System.Drawing.Rectangle(0, $bmp.Height - 60, $bmp.Width, 60)
    $out = $bmp.Clone($strip, $bmp.PixelFormat)
    $out.Save($Path, [System.Drawing.Imaging.ImageFormat]::Png)
    $out.Dispose(); $bmp.Dispose()
}

function Shot-08RestoreSteps {
    param([string]$Path)
    $win = Find-AppWindow
    $rows = $win.FindAll([System.Windows.Automation.TreeScope]::Descendants,
        (New-Object System.Windows.Automation.PropertyCondition(
            [System.Windows.Automation.AutomationElement]::ControlTypeProperty,
            [System.Windows.Automation.ControlType]::Custom)))
    $restore = $null
    foreach ($row in $rows) {
        if ($row.Current.Name -match 'Large Save Game') {
            $restore = Find-ButtonByName $row 'Restore'
            break
        }
    }
    if (-not $restore) { throw 'Large Save Game row Restore button not found' }
    [void](Invoke-Element $restore)
    Wait-Seconds 1
    # Restore asks per-game confirmation first — accept it with the confirm button.
    $confirm = Find-ButtonByName (Find-AppWindow) 'Restore'
    if ($confirm) { [void](Invoke-Element $confirm) }
    # The three-step bar and step text run for seconds on the 1 GiB archive — shoot mid-flight.
    Wait-Seconds 2
    Save-WindowShot $Path
    Wait-Seconds 20
    # Decline the confirmation afterwards (archive delete / completion prompt) with Cancel.
    [void](Confirm-Dialog 'Cancel')
    Wait-Seconds 1
    [void](Confirm-Dialog 'Cancel')
}

function Shot-09EmptyState {
    param([string]$Path)
    Stop-App
    Invoke-DemoResetWithRoot $Root | Out-Null
    if (-not (Start-App (Join-Path $Root 'empty.json'))) { throw 'the app window did not appear after launching with empty.json' }
    Wait-Seconds 2
    Dismiss-StartupPrompt   # empty.json has no games, so no prompt appears; harmless
    Save-WindowShot $Path
    Stop-App
    # Back to the four-game catalog for the remaining dialog shots.
    Invoke-DemoResetWithRoot $Root | Out-Null
    if (-not (Start-App (Join-Path $Root 'games.json'))) { throw 'the app window did not appear after relaunching with games.json' }
    Wait-Seconds 2
    Dismiss-StartupPrompt
}

function Shot-10AddGameFooter {
    param([string]$Path)
    $win = Find-AppWindow
    $btn = Find-ElementById $win 'AddGameButton'
    if (-not $btn -or -not (Invoke-Element $btn)) { throw 'AddGameButton not found' }
    Wait-Seconds 2
    # Type a name that collides with an existing game so the validation message shows.
    [System.Windows.Forms.SendKeys]::SendWait('Small Save Game')
    Wait-Seconds 1
    Save-WindowShot $Path
    [void](Confirm-Dialog 'Cancel')
    Wait-Seconds 1
}

function Shot-11ChangeSaveFolderFooter {
    param([string]$Path)
    $win = Find-AppWindow
    $more = Find-ButtonByName $win 'More actions'
    if (-not $more) { throw 'a row More actions button not found' }
    [void](Invoke-Element $more)
    Wait-Seconds 1
    $item = Find-ButtonByName $win 'Change save folder'
    if (-not $item) { throw 'Change save folder menu item not found' }
    [void](Invoke-Element $item)
    Wait-Seconds 2
    Save-WindowShot $Path
    [void](Confirm-Dialog 'Cancel')
    Wait-Seconds 1
}

function Shot-12ScanFooter {
    param([string]$Path)
    $win = Find-AppWindow
    $btn = Find-ElementById $win 'FindGamesButton'
    if (-not $btn -or -not (Invoke-Element $btn)) { throw 'FindGamesButton not found' }
    Wait-Seconds 3
    # The scan runs first; its window appears when it finishes. On a machine where no known
    # game is found the window may never show — the shot is then MISSING with that reason.
    $scan = $null
    for ($i = 0; $i -lt 60; $i++) {
        Start-Sleep -Milliseconds 500
        $proc = Get-Process -Name 'SavegameBunker' -ErrorAction SilentlyContinue |
            Where-Object { $_.MainWindowHandle -ne 0 } | Select-Object -First 1
        if (-not $proc) { continue }
        $all = [System.Windows.Automation.AutomationElement]::RootElement.FindAll(
            [System.Windows.Automation.TreeScope]::Children,
            [System.Windows.Automation.Condition]::TrueCondition)
        foreach ($top in $all) {
            if ($top.Current.Name -and $top.Current.Name -ne $win.Current.Name) { $scan = $top; break }
        }
        if ($scan) { break }
    }
    if (-not $scan) { throw 'no scan window appeared (no known game found on this machine)' }
    Save-WindowShot $Path
    [System.Windows.Forms.SendKeys]::SendWait('{ESC}')
    Wait-Seconds 1
}

function Shot-13RestorePickerFooter {
    param([string]$Path)
    $win = Find-AppWindow
    # "Small Save Game" was backed up at run start, so its Restore opens the restore picker.
    $rows = $win.FindAll([System.Windows.Automation.TreeScope]::Descendants,
        (New-Object System.Windows.Automation.PropertyCondition(
            [System.Windows.Automation.AutomationElement]::ControlTypeProperty,
            [System.Windows.Automation.ControlType]::Custom)))
    $restore = $null
    foreach ($row in $rows) {
        if ($row.Current.Name -match 'Small Save Game') {
            $restore = Find-ButtonByName $row 'Restore'
            break
        }
    }
    if (-not $restore) { throw 'Small Save Game row Restore button not found' }
    [void](Invoke-Element $restore)
    Wait-Seconds 2
    # If a picker window opened, shoot it; if restore went straight to the confirm dialog,
    # this environment shows no picker and the shot is MISSING.
    $proc = Get-Process -Name 'SavegameBunker' -ErrorAction SilentlyContinue |
        Where-Object { $_.MainWindowHandle -ne 0 } | Select-Object -First 1
    $picker = $null
    $all = [System.Windows.Automation.AutomationElement]::RootElement.FindAll(
        [System.Windows.Automation.TreeScope]::Children,
        [System.Windows.Automation.Condition]::TrueCondition)
    foreach ($top in $all) {
        if ($top.Current.Name -and $top.Current.Name -ne $win.Current.Name) { $picker = $top; break }
    }
    if ($picker) { Save-WindowShot $Path } else { throw 'no restore picker window appeared' }
    [void](Confirm-Dialog 'Cancel')
    Wait-Seconds 1
    [void](Confirm-Dialog 'Cancel')
}

function Shot-14CloseHover {
    param([string]$Path)
    $proc = Get-Process -Name 'SavegameBunker' | Where-Object { $_.MainWindowHandle -ne 0 } | Select-Object -First 1
    $rect = New-Object Win32.Native+RECT
    [Win32.Native]::GetWindowRect($proc.MainWindowHandle, [ref]$rect) | Out-Null
    # Move the cursor over the title bar's close button (top-right corner of the window).
    [Win32.Native]::SetCursorPos($rect.Right - 12, $rect.Top + 12) | Out-Null
    Start-Sleep -Milliseconds 500
    Save-WindowShot $Path
}

# ---------------------------------------------------------------------------
# Run
# ---------------------------------------------------------------------------

if (-not (Test-Path $OutDir)) { New-Item -ItemType Directory -Path $OutDir -Force | Out-Null }

Invoke-DemoResetWithRoot $Root

if (-not (Start-App (Join-Path $Root 'games.json'))) {
    Write-Error 'FATAL: the app could not be launched or its window was not found.'
    exit 1
}
Wait-Seconds 2
Dismiss-StartupPrompt

# Back up "Small Save Game" once so restore shots (08, 13) have a backup to restore from.
[void](Backup-SmallGameRow)
Wait-Seconds 5
Dismiss-StartupPrompt   # a snackbar or follow-up dialog, if any, is not part of a shot

foreach ($shot in $script:ShotList) {
    $path = Join-Path $OutDir ($shot + '.png')
    $fn = $null
    switch ($shot) {
        '01-main-default'              { $fn = { param($p) Shot-01MainDefault $p } }
        '02-main-other-theme'          { $fn = { param($p) Shot-02MainOtherTheme $p } }
        '03-toolbar-alt'               { $fn = { param($p) Shot-03ToolbarAlt $p } }
        '04-overflow-menu'             { $fn = { param($p) Shot-04OverflowMenu $p } }
        '05-row-actions-idle'          { $fn = { param($p) Shot-05RowActionsIdle $p } }
        '06-busy-bulk'                 { $fn = { param($p) Shot-06BusyBulk $p } }
        '07-statusbar'                 { $fn = { param($p) Shot-07Statusbar $p } }
        '08-restore-steps'             { $fn = { param($p) Shot-08RestoreSteps $p } }
        '09-empty-state'               { $fn = { param($p) Shot-09EmptyState $p } }
        '10-addgame-footer'            { $fn = { param($p) Shot-10AddGameFooter $p } }
        '11-changesavefolder-footer'   { $fn = { param($p) Shot-11ChangeSaveFolderFooter $p } }
        '12-scan-footer'               { $fn = { param($p) Shot-12ScanFooter $p } }
        '13-restorepicker-footer'      { $fn = { param($p) Shot-13RestorePickerFooter $p } }
        '14-close-hover'               { $fn = { param($p) Shot-14CloseHover $p } }
        default                        { Record-Missing $shot 'unknown shot name'; continue }
    }
    try {
        & $fn $path
        if (Test-Path $path) { Record-Ok $shot } else { Record-Missing $shot 'no image was written' }
    } catch {
        Record-Missing $shot $_.Exception.Message
        # Recover to a known state so the next shot is not taken from a surprise dialog.
        Wait-Seconds 1
        [void](Confirm-Dialog 'Cancel')
        [System.Windows.Forms.SendKeys]::SendWait('{ESC}')
        Wait-Seconds 1
    }
}

Stop-App

if ($script:Missing.Count -gt 0) {
    Write-Host ("Missing shots: " + ($script:Missing -join ', '))
    Write-Host 'Done — some shots are missing (exit 0).'
    exit 0
}
Write-Host 'Done — all shots taken.'
exit 0
