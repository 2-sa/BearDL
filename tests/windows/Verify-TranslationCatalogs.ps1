param(
    [Parameter(Mandatory)][string]$PublishDirectory,
    [string]$RepositoryRoot = (Join-Path $PSScriptRoot '../..')
)

$ErrorActionPreference = 'Stop'
$identitySource = Get-Content -LiteralPath (Join-Path $RepositoryRoot 'Nickvision.Parabolic.Shared/Helpers/HostApplicationBuilderExtensions.cs') -Raw
$identity = [regex]::Match($identitySource, 'new AppInfo\("[^"]+", "[^"]+", "([^"]+)"\)')
if (-not $identity.Success) {
    throw 'Cannot determine the application translation domain from its identity.'
}
# Nickvision.Desktop 2026.5.3 TranslationService uses EnglishShortName here.
$domain = $identity.Groups[1].Value.Replace(' ', '').ToLowerInvariant()
$languages = Get-Content -LiteralPath (Join-Path $RepositoryRoot 'resources/po/LINGUAS') |
    Where-Object { $_.Trim() -and -not $_.Trim().StartsWith('#') }
foreach ($language in $languages) {
    $catalogPath = Join-Path $PublishDirectory "$($language.Trim())/$domain.mo"
    if (-not (Test-Path -LiteralPath $catalogPath -PathType Leaf)) {
        throw "The runtime cannot discover language '$language': missing $domain.mo in the published output."
    }
    $catalogBytes = [System.IO.File]::ReadAllBytes($catalogPath)
    if ($catalogBytes.Length -lt 28 -or [BitConverter]::ToUInt32($catalogBytes, 0) -notin @(0x950412deL, 0xde120495L)) {
        throw "Invalid compiled translation catalog for '$language'."
    }
}
$arabicCatalog = [System.IO.File]::ReadAllBytes((Join-Path $PublishDirectory "ar/$domain.mo"))
if (-not [System.Text.Encoding]::UTF8.GetString($arabicCatalog).Contains('الإعدادات')) {
    throw 'The published Arabic catalog does not contain the Settings translation.'
}
$arabicText = [System.Text.Encoding]::UTF8.GetString($arabicCatalog)
foreach ($requiredTranslation in @('تصميم الواجهة', 'كلاسيكي', 'حديث', 'المفاضلة بين التصميم الكلاسيكي والحديث للواجهة')) {
    if (-not $arabicText.Contains($requiredTranslation)) {
        throw "The published Arabic catalog is missing a layout translation: $requiredTranslation"
    }
}
Write-Output "Verified $($languages.Count) published languages against runtime domain '$domain', including Arabic."
