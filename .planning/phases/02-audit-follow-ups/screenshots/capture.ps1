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
# "More actions") found inside the row's own ControlType.DataItem via its nested display-name
# Text descendant (Find-RowButton), never a position guess, dialogs by their button text
# ("Back up now", "Cancel", "OK"). Dialogs, the restore picker and the overflow menu are owned
# windows or a separate popup root, so they are found by scanning the SavegameBunker process's
# own top-level elements (Find-AppDialogWindow, Find-AppMenuItem) rather than $win's descendants.
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
[System.Runtime.InteropServices.DllImport("user32.dll")]
public static extern bool SetForegroundWindow(System.IntPtr hWnd);
[System.Runtime.InteropServices.DllImport("user32.dll")]
public static extern System.IntPtr GetForegroundWindow();
[System.Runtime.InteropServices.DllImport("user32.dll")]
public static extern bool PostMessage(System.IntPtr hWnd, uint msg, System.IntPtr wParam, System.IntPtr lParam);
public struct RECT { public int Left, Top, Right, Bottom; }
"@

$script:ShotList = $Shots -split ',' | ForEach-Object { $_.Trim() } | Where-Object { $_ }
$script:Missing = @()

# ---------------------------------------------------------------------------
# Helpers
# ---------------------------------------------------------------------------

function Set-ForegroundReliable {
    # Plain SetForegroundWindow can be silently denied by Windows' foreground-lock timeout
    # when the calling process did not generate the most recent input — exactly what happens
    # after this script's own multi-second Wait-Seconds calls while the desktop is used for
    # anything else. Found while verifying this plan against an older build: a capture taken
    # right after such a wait held a wholly unrelated window's content (T-02-31), because this
    # call had silently done nothing. Tapping Alt first is the standard workaround: it gives
    # this process a fresh input event, which Windows accepts as grounds to grant the request.
    param([System.IntPtr]$HWnd)
    [Win32.Native]::keybd_event(0x12, 0, 0, [System.IntPtr]::Zero)        # VK_MENU down
    [Win32.Native]::keybd_event(0x12, 0, 2, [System.IntPtr]::Zero)        # VK_MENU up (KEYEVENTF_KEYUP)
    [void][Win32.Native]::SetForegroundWindow($HWnd)
}

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

function Find-RowButton {
    # The grid's rows are ControlType.DataItem elements whose own Name is the row view model's
    # type name; each cell is a generic element whose Name is a Windows-localised
    # "item, column index" label — the display name exists only as a nested ControlType.Text
    # descendant. So the row is the DataItem that has a Text descendant whose Name equals
    # $DisplayName exactly, and the button is Find-ButtonByName inside that same DataItem.
    # Returns $null when either the row or the button is not found (no positional fallback).
    param($Root, [string]$DisplayName, [string]$ButtonName)
    $rows = $Root.FindAll([System.Windows.Automation.TreeScope]::Descendants,
        (New-Object System.Windows.Automation.PropertyCondition(
            [System.Windows.Automation.AutomationElement]::ControlTypeProperty,
            [System.Windows.Automation.ControlType]::DataItem)))
    foreach ($row in $rows) {
        $text = $row.FindFirst([System.Windows.Automation.TreeScope]::Descendants,
            (New-Object System.Windows.Automation.AndCondition(
                (New-Object System.Windows.Automation.PropertyCondition(
                    [System.Windows.Automation.AutomationElement]::ControlTypeProperty,
                    [System.Windows.Automation.ControlType]::Text)),
                (New-Object System.Windows.Automation.PropertyCondition(
                    [System.Windows.Automation.AutomationElement]::NameProperty, $DisplayName)))))
        if ($text) { return (Find-ButtonByName $row $ButtonName) }
    }
    return $null
}

function Find-AppDialogWindow {
    # Dialogs and the restore picker are owned top-level windows, and the overflow menu is a
    # separate popup root, so none of them can be relied on to sit inside the main window's own
    # subtree. Searches the SavegameBunker process's own elements instead: the RootElement
    # children whose ProcessIdProperty matches the process that has a main window, checking each
    # such element itself and its ControlType.Window descendants (UI Automation may present an
    # owned window under its owner — 02-16-SUMMARY.md) for a Name starting with $TitlePrefix.
    param([string]$TitlePrefix)
    $proc = Get-Process -Name 'SavegameBunker' -ErrorAction SilentlyContinue |
        Where-Object { $_.MainWindowHandle -ne 0 } | Select-Object -First 1
    if (-not $proc) { return $null }
    $pidCondition = New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::ProcessIdProperty, $proc.Id)
    $tops = [System.Windows.Automation.AutomationElement]::RootElement.FindAll(
        [System.Windows.Automation.TreeScope]::Children, $pidCondition)
    foreach ($top in $tops) {
        if ($top.Current.Name -and $top.Current.Name.StartsWith($TitlePrefix, [System.StringComparison]::Ordinal)) {
            return $top
        }
        $windowCondition = New-Object System.Windows.Automation.AndCondition(
            $pidCondition,
            (New-Object System.Windows.Automation.PropertyCondition(
                [System.Windows.Automation.AutomationElement]::ControlTypeProperty,
                [System.Windows.Automation.ControlType]::Window)))
        $owned = $top.FindAll([System.Windows.Automation.TreeScope]::Descendants, $windowCondition)
        foreach ($win in $owned) {
            if ($win.Current.Name -and $win.Current.Name.StartsWith($TitlePrefix, [System.StringComparison]::Ordinal)) {
                return $win
            }
        }
    }
    return $null
}

function Find-AppMenuItem {
    # The overflow menu (ui:DropDownButton.Flyout) renders in its own popup root, outside $win's
    # subtree entirely (MainWindow.xaml's comment above the ContextMenu) — found the same
    # process-scoped way as Find-AppDialogWindow. A MenuItem's Header is a plain string at HEAD;
    # before item 19's HeaderTemplate (57811c1) it was a panel holding a TextBlock, so a
    # MenuItem's own Name can be empty while a nested Text child carries it instead.
    param([string]$Text)
    $proc = Get-Process -Name 'SavegameBunker' -ErrorAction SilentlyContinue |
        Where-Object { $_.MainWindowHandle -ne 0 } | Select-Object -First 1
    if (-not $proc) { return $null }
    $pidCondition = New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::ProcessIdProperty, $proc.Id)
    $tops = [System.Windows.Automation.AutomationElement]::RootElement.FindAll(
        [System.Windows.Automation.TreeScope]::Children, $pidCondition)
    $menuItemCondition = New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::ControlTypeProperty,
        [System.Windows.Automation.ControlType]::MenuItem)
    foreach ($top in $tops) {
        $items = $top.FindAll([System.Windows.Automation.TreeScope]::Descendants, $menuItemCondition)
        foreach ($item in $items) {
            if ($item.Current.Name -eq $Text) { return $item }
            $nested = $item.FindFirst([System.Windows.Automation.TreeScope]::Descendants,
                (New-Object System.Windows.Automation.AndCondition(
                    (New-Object System.Windows.Automation.PropertyCondition(
                        [System.Windows.Automation.AutomationElement]::ControlTypeProperty,
                        [System.Windows.Automation.ControlType]::Text)),
                    (New-Object System.Windows.Automation.PropertyCondition(
                        [System.Windows.Automation.AutomationElement]::NameProperty, $Text)))))
            if ($nested) { return $item }
        }
    }
    return $null
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

function Invoke-RealClick {
    # The overflow DropDownButton exposes InvokePattern, but invoking it does not open its
    # Flyout in this environment (found while verifying this plan's full fourteen-shot run:
    # shot 04's image showed no overflow menu at all) - only a real mouse click does, and only
    # when mouse-down and mouse-up are separated by a short delay; an immediate down+up does not
    # register as a click with WPF's popup-opening logic. Used for "More actions" instead of
    # Invoke-Element, which would always take the InvokePattern branch first.
    param($Element)
    if (-not $Element) { return $false }
    $r = $Element.Current.BoundingRectangle
    if ($r -eq [System.Windows.Rect]::Empty) { return $false }
    [Win32.Native]::SetCursorPos([int]($r.Left + $r.Width / 2), [int]($r.Top + $r.Height / 2)) | Out-Null
    [Win32.Native]::mouse_event(2, 0, 0, 0, [System.IntPtr]::Zero)   # left down
    Start-Sleep -Milliseconds 100
    [Win32.Native]::mouse_event(4, 0, 0, 0, [System.IntPtr]::Zero)   # left up
    return $true
}

function Save-WindowShot {
    # Copy only the app window's rectangle from the screen — never the whole desktop.
    param([string]$Path)
    $proc = Get-Process -Name 'SavegameBunker' -ErrorAction SilentlyContinue |
        Where-Object { $_.MainWindowHandle -ne 0 } | Select-Object -First 1
    if (-not $proc) { throw 'SavegameBunker window not found' }
    # CopyFromScreen copies whatever is topmost on screen, not the app's own rendered content —
    # on a shared desktop another window can occlude it between shots, especially across a long
    # wait (T-02-31; hit once during this plan's own verification, after the restore safety
    # backup's ~20s wait). Raising the app window first makes that the topmost content again.
    Set-ForegroundReliable $proc.MainWindowHandle
    Start-Sleep -Milliseconds 200
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
    # Same reason as Save-WindowShot: raise the window before copying screen pixels (T-02-31).
    Set-ForegroundReliable $proc.MainWindowHandle
    Start-Sleep -Milliseconds 200
    $wrect = New-Object Win32.Native+RECT
    [Win32.Native]::GetWindowRect($proc.MainWindowHandle, [ref]$wrect) | Out-Null
    $er = $Element.Current.BoundingRectangle
    # Parenthesize each subtraction: inside a method-argument list, PowerShell's comma binds
    # tighter than `-`, so an unparenthesized `a - b, c - d` becomes an array literal minus a
    # scalar and throws op_Subtraction (G-02-7).
    $bmp = New-Object System.Drawing.Bitmap(($wrect.Right - $wrect.Left), ($wrect.Bottom - $wrect.Top))
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
    # Back up "Small Save Game" once through its row Back up button, so later shots have a
    # backup to restore from. The row is found via Find-RowButton (DataItem + nested Text), not
    # a position guess — a positional fallback backs up whichever row happens to sit there.
    $win = Find-AppWindow
    $btn = Find-RowButton $win 'Small Save Game' 'Back up'
    if ($btn) { [void](Invoke-Element $btn); return $true }
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
    if (-not $btn) { throw 'ThemeToggleButton not found' }
    # Found while verifying this plan's full fourteen-shot run: InvokePattern.Invoke() does not
    # reliably trigger this button's Click handler in this environment (same symptom as "More
    # actions" - see Invoke-RealClick). A real click is itself occasionally missed on this host
    # even with the delay Invoke-RealClick adds, so compare the window's pixels before/after and
    # retry the click (never the opposite direction - a second successful click would toggle the
    # theme straight back) until something visibly changed.
    $beforePath = [System.IO.Path]::GetTempFileName()
    Save-WindowShot $beforePath
    $beforeHash = (Get-FileHash $beforePath -Algorithm MD5).Hash
    Remove-Item $beforePath -Force -ErrorAction SilentlyContinue
    for ($attempt = 0; $attempt -lt 3; $attempt++) {
        [void](Invoke-RealClick $btn)
        Wait-Seconds 1
        Save-WindowShot $Path
        if ((Get-FileHash $Path -Algorithm MD5).Hash -ne $beforeHash) { break }
    }
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
    # The real click Invoke-RealClick sends is itself occasionally missed on this host - confirm
    # the flyout actually opened (its own menu item is findable) before screenshotting. Each
    # retry gets its own poll window before the NEXT click: the DropDownButton toggles, so a
    # second click sent while the first one's flyout is just slow to register would close it
    # again instead of giving the lookup more time to find it.
    $item = $null
    for ($attempt = 0; $attempt -lt 3 -and -not $item; $attempt++) {
        [void](Invoke-RealClick $more)
        for ($i = 0; $i -lt 6 -and -not $item; $i++) {
            Start-Sleep -Milliseconds 300
            $item = Find-AppMenuItem 'Change save folder'
        }
    }
    if (-not $item) { throw 'overflow menu did not open' }
    Save-WindowShot $Path
    [System.Windows.Forms.SendKeys]::SendWait('{ESC}')
    Wait-Seconds 1
}

function Shot-05RowActionsIdle {
    param([string]$Path)
    $win = Find-AppWindow
    $more = Find-RowButton $win 'Large Save Game' 'More actions'
    if (-not $more) { throw 'Large Save Game row More actions button not found' }
    # The Actions cell's StackPanel has no automation peer, so walking up from the button one
    # control-view step lands on the cell (or, where that peer is also absent, the row) — either
    # way the crop includes all three idle action buttons, not just this one.
    $cell = [System.Windows.Automation.TreeWalker]::ControlViewWalker.GetParent($more)
    Save-ElementShot $cell $Path
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
    # Same reason as Save-WindowShot/Save-ElementShot (T-02-31): raise the window before
    # copying screen pixels, or another window can occlude the captured region on a shared
    # desktop. Found while verifying this plan against an older build: without this, the
    # cropped strip sometimes held desktop content instead of the status bar.
    Set-ForegroundReliable $proc.MainWindowHandle
    Start-Sleep -Milliseconds 200
    $rect = New-Object Win32.Native+RECT
    [Win32.Native]::GetWindowRect($proc.MainWindowHandle, [ref]$rect) | Out-Null
    $w = $rect.Right - $rect.Left
    # Parenthesized for the same op_Subtraction reason as Save-ElementShot above (G-02-7).
    $bmp = New-Object System.Drawing.Bitmap($w, ($rect.Bottom - $rect.Top))
    $gfx = [System.Drawing.Graphics]::FromImage($bmp)
    $gfx.CopyFromScreen($rect.Left, $rect.Top, 0, 0, $bmp.Size)
    $gfx.Dispose()
    $strip = New-Object System.Drawing.Rectangle(0, ($bmp.Height - 60), $bmp.Width, 60)
    $out = $bmp.Clone($strip, $bmp.PixelFormat)
    $out.Save($Path, [System.Drawing.Imaging.ImageFormat]::Png)
    $out.Dispose(); $bmp.Dispose()
}

function Shot-08RestoreSteps {
    param([string]$Path)
    $win = Find-AppWindow
    $restore = Find-RowButton $win 'Large Save Game' 'Restore'
    if (-not $restore) { throw 'Large Save Game row Restore button not found' }
    [void](Invoke-Element $restore)
    # A row's Restore always opens the restore picker first; its own Restore stays disabled
    # until a row is selected. The per-game confirmation comes after the picker closes, in the
    # main window's dialog host, and its primary button is also named "Restore".
    $picker = $null
    for ($i = 0; $i -lt 20; $i++) {
        Start-Sleep -Milliseconds 500
        $picker = Find-AppDialogWindow ('Restore ' + [char]0x2014 + ' Large Save Game')
        if ($picker) { break }
    }
    if (-not $picker) { throw 'no restore picker window appeared for Large Save Game' }
    $item = $picker.FindFirst([System.Windows.Automation.TreeScope]::Descendants,
        (New-Object System.Windows.Automation.PropertyCondition(
            [System.Windows.Automation.AutomationElement]::ControlTypeProperty,
            [System.Windows.Automation.ControlType]::ListItem)))
    if (-not $item) { throw 'no archive listed in the restore picker for Large Save Game' }
    $item.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select()
    $pickerRestore = Find-ButtonByName $picker 'Restore'
    if (-not $pickerRestore) { throw 'restore picker Restore button not found' }
    [void](Invoke-Element $pickerRestore)
    # Wait for the per-game confirmation (only an open content dialog gives the main window a
    # Cancel button), then accept it: the LAST Button named "Restore" among the main window's
    # descendants — RootDialogHost is the root Grid's last child, so a dialog's buttons follow
    # every row button in tree order, and every row's own Restore button shares this same Name.
    # A safety backup of the save folder is taken before the confirmation shows (restore order,
    # MainWindow.xaml.cs comment), so for the 1 GiB archive this can take upwards of 20s - the
    # poll window below is sized for that, not just for the confirmation to render.
    $confirmed = $false
    for ($i = 0; $i -lt 120; $i++) {
        Start-Sleep -Milliseconds 500
        $mainWin = Find-AppWindow
        if (-not (Find-ButtonByName $mainWin 'Cancel')) { continue }
        $restores = $mainWin.FindAll([System.Windows.Automation.TreeScope]::Descendants,
            (New-Object System.Windows.Automation.AndCondition(
                (New-Object System.Windows.Automation.PropertyCondition(
                    [System.Windows.Automation.AutomationElement]::NameProperty, 'Restore')),
                (New-Object System.Windows.Automation.PropertyCondition(
                    [System.Windows.Automation.AutomationElement]::ControlTypeProperty,
                    [System.Windows.Automation.ControlType]::Button)))))
        if ($restores.Count -eq 0) { continue }
        [void](Invoke-Element $restores[$restores.Count - 1])
        $confirmed = $true
        break
    }
    if (-not $confirmed) { throw 'restore confirmation dialog did not appear' }
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
    # The demo reset above emptied backups/, so without repeating the run prologue's backup
    # here, Small Save Game would have no archive and Shot 13 would have nothing to restore.
    if (-not (Backup-SmallGameRow)) {
        Write-Host "WARNING: Small Save Game row Back up button not found - shot 13 will have no backup to restore"
    }
    Wait-Seconds 5
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
    # "Change save folder" lives in the DropDownButton's own flyout, a separate popup root
    # (MainWindow.xaml's comment above the ContextMenu) — found by Find-AppMenuItem, not a
    # Button search scoped to $win. The real click Invoke-RealClick sends is itself occasionally
    # missed on this host; each retry gets its own poll window before the next click, same
    # reason as Shot-04 (the DropDownButton toggles, so clicking again too soon closes it).
    $item = $null
    for ($attempt = 0; $attempt -lt 3 -and -not $item; $attempt++) {
        [void](Invoke-RealClick $more)
        for ($i = 0; $i -lt 6 -and -not $item; $i++) {
            Start-Sleep -Milliseconds 300
            $item = Find-AppMenuItem 'Change save folder'
        }
    }
    if (-not $item) { throw 'Change save folder menu item not found' }
    [void](Invoke-Element $item)
    $dialog = $null
    for ($i = 0; $i -lt 20; $i++) {
        Start-Sleep -Milliseconds 500
        $dialog = Find-AppDialogWindow 'Change Save Folder'
        if ($dialog) { break }
    }
    if (-not $dialog) { throw 'Change Save Folder dialog did not appear' }
    Save-WindowShot $Path
    $cancel = Find-ButtonByName $dialog 'Cancel'
    if ($cancel) { [void](Invoke-Element $cancel) }
    for ($i = 0; $i -lt 20; $i++) {
        Start-Sleep -Milliseconds 500
        if (-not (Find-AppDialogWindow 'Change Save Folder')) { return }
    }
    throw 'Change Save Folder dialog did not close'
}

function Shot-12ScanFooter {
    param([string]$Path)
    $win = Find-AppWindow
    $btn = Find-ElementById $win 'FindGamesButton'
    if (-not $btn -or -not (Invoke-Element $btn)) { throw 'FindGamesButton not found' }
    Wait-Seconds 3
    # The scan runs first; its window appears when it finishes. On a machine where no known
    # game is found the window may never show — the shot is then MISSING with that reason.
    # Found while verifying this plan's full fourteen-shot run: matching "the first top-level
    # window whose Name differs from the main window's" picked up an unrelated window on a
    # shared desktop instead of the real Scan window — the screenshot still looked right (it
    # always captures the main app window's rect, which the real Scan window renders over
    # regardless of which element this code thought it was), but every later attempt to close
    # "the scan window" acted on that wrong element and did nothing, leaking the real dialog into
    # shots 13 and 14. Find-AppDialogWindow scopes the search to this process's own windows by
    # title instead, the same way every other dialog shot already finds its window.
    $scan = $null
    for ($i = 0; $i -lt 60; $i++) {
        Start-Sleep -Milliseconds 500
        $scan = Find-AppDialogWindow 'Scan'
        if ($scan) { break }
    }
    if (-not $scan) { throw 'no scan window appeared (no known game found on this machine)' }
    $scanName = $scan.Current.Name
    $scanHandle = [System.IntPtr]$scan.Current.NativeWindowHandle
    Save-WindowShot $Path
    # Clicking the scan window's own Cancel button via UI Automation's Invoke pattern, and
    # SendKeys {ESC}, both left this window open (SendKeys needs this window to hold input focus,
    # which an automated run on a shared desktop cannot guarantee). Closing the real HWND with
    # WM_CLOSE does not depend on focus or on the Cancel button's automation peer.
    if ($scanHandle -ne [System.IntPtr]::Zero) {
        [void][Win32.Native]::PostMessage($scanHandle, 0x0010, [System.IntPtr]::Zero, [System.IntPtr]::Zero) # WM_CLOSE
    }
    $closed = $false
    for ($i = 0; $i -lt 20; $i++) {
        Start-Sleep -Milliseconds 300
        if (-not (Find-AppDialogWindow $scanName)) { $closed = $true; break }
    }
    if (-not $closed) { throw 'Scan window did not close' }
}

function Shot-13RestorePickerFooter {
    param([string]$Path)
    $win = Find-AppWindow
    # "Small Save Game" was backed up at run start, so its Restore opens the restore picker.
    $restore = Find-RowButton $win 'Small Save Game' 'Restore'
    if (-not $restore) { throw 'Small Save Game row Restore button not found' }
    [void](Invoke-Element $restore)
    # Found by Find-AppDialogWindow, not "any other named top-level window" — that test accepted
    # any unrelated window on the desktop.
    $picker = $null
    for ($i = 0; $i -lt 20; $i++) {
        Start-Sleep -Milliseconds 500
        $picker = Find-AppDialogWindow ('Restore ' + [char]0x2014 + ' Small Save Game')
        if ($picker) { break }
    }
    if (-not $picker) { throw 'no restore picker window appeared' }
    Save-WindowShot $Path
    $cancel = Find-ButtonByName $picker 'Cancel'
    if ($cancel) { [void](Invoke-Element $cancel) }
    for ($i = 0; $i -lt 20; $i++) {
        Start-Sleep -Milliseconds 500
        if (-not (Find-AppDialogWindow ('Restore ' + [char]0x2014 + ' Small Save Game'))) { return }
    }
    throw 'restore picker window did not close'
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
if (-not (Backup-SmallGameRow)) {
    Write-Host "WARNING: Small Save Game row Back up button not found - shot 13 will have no backup to restore"
}
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
