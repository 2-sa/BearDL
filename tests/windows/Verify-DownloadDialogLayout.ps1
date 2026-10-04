param([int]$AppProcessId = 0)

# Run with the installed app's single-download configuration dialog open.
# This is read-only: it does not click controls or change downloads/settings.
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
if (-not ('BearDLWindowDpi' -as [type])) {
    Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class BearDLWindowDpi
{
    [DllImport("user32.dll")]
    public static extern uint GetDpiForWindow(IntPtr window);
}
'@
}

$appProcess = if ($AppProcessId) {
    Get-Process -Id $AppProcessId
} else {
    Get-Process -Name 'Nickvision.Parabolic.WinUI' | Select-Object -First 1
}
$root = [System.Windows.Automation.AutomationElement]::FromHandle($appProcess.MainWindowHandle)

function Find-DialogElement([string]$Id) {
    $condition = [System.Windows.Automation.PropertyCondition]::new(
        [System.Windows.Automation.AutomationElement]::AutomationIdProperty, $Id)
    $element = $root.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $condition)
    if (-not $element) { throw "Open the single-download configuration dialog first: missing $Id" }
    return $element
}

$left = (Find-DialogElement 'BtnSingleRevertFilename').Current.BoundingRectangle.Left
$right = (Find-DialogElement 'TxtSingleSaveFolder').Current.BoundingRectangle.Right
$failures = @()
$thumbnailWidth = (Find-DialogElement 'ImgSingleThumbnail').Current.BoundingRectangle.Width
$scale = [BearDLWindowDpi]::GetDpiForWindow($appProcess.MainWindowHandle) / 96.0
$expectedThumbnailWidth = 150 * $scale
if ($thumbnailWidth -lt ($expectedThumbnailWidth - 2)) {
    $failures += "Thumbnail is clipped (visible width $thumbnailWidth, expected $expectedThumbnailWidth)."
}
foreach ($id in @('ImgSingleThumbnail', 'NavViewItemSingleGeneral',
                   'CmbSingleFileType', 'CmbSingleVideoFormat', 'CmbSingleAudioFormat')) {
    $element = Find-DialogElement $id
    $bounds = $element.Current.BoundingRectangle
    if ($element.Current.IsOffscreen -or $bounds.IsEmpty) {
        $failures += "$id is outside the visible dialog."
    } elseif ($bounds.Left -lt ($left - 2) -or $bounds.Right -gt ($right + 2)) {
        $failures += "$id extends outside the content area (width $($bounds.Width), available $($right - $left))."
    }
}
if ($failures.Count) { throw ($failures -join [Environment]::NewLine) }
Write-Output 'PASS: thumbnail, tabs, and format controls fit the visible dialog.'
