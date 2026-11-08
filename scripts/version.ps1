<#
.SYNOPSIS
    Bump version for OpenBVE-Android releases
.DESCRIPTION
    Updates version.txt and creates a git tag
.PARAMETER Type
    Version bump type: major, minor, patch
.EXAMPLE
    .\scripts\version.ps1 -Type patch
    .\scripts\version.ps1 -Type minor
    .\scripts\version.ps1 -Type major
#>

param(
    [Parameter(Mandatory=$true)]
    [ValidateSet('major', 'minor', 'patch')]
    [string]$Type
)

$versionFile = "version.txt"
$currentVersion = Get-Content $versionFile -Raw
$parts = $currentVersion -split '\.'

if ($parts.Count -ne 3) {
    Write-Error "Invalid version format: $currentVersion"
    exit 1
}

[Version]$v = $currentVersion

switch ($Type) {
    'major' { $newVersion = [Version]::new($v.Major + 1, 0, 0) }
    'minor' { $newVersion = [Version]::new($v.Major, $v.Minor + 1, 0) }
    'patch' { $newVersion = [Version]::new($v.Major, $v.Minor, $v.Build + 1) }
}

$newVersionStr = "$($newVersion.Major).$($newVersion.Minor).$($newVersion.Build)"
Write-Host "Bumping version: $currentVersion -> $newVersionStr"

$newVersionStr | Set-Content $versionFile -NoNewline

# Update AndroidManifest.xml versionCode/versionName
$manifest = "android/src/main/AndroidManifest.xml"
if (Test-Path $manifest) {
    $content = Get-Content $manifest -Raw
    $content = $content -replace 'android:versionCode="\d+"', "android:versionCode=`"$($newVersion.Major * 10000 + $newVersion.Minor * 100 + $newVersion.Build)`""
    $content = $content -replace 'android:versionName="[^"]*"', "android:versionName=`"$newVersionStr`""
    $content | Set-Content $manifest
    Write-Host "Updated $manifest"
}

# Update build.gradle version
$buildGradle = "android/app/build.gradle"
if (Test-Path $buildGradle) {
    $content = Get-Content $buildGradle -Raw
    $content = $content -replace 'versionCode \d+', "versionCode $($newVersion.Major * 10000 + $newVersion.Minor * 100 + $newVersion.Build)"
    $content = $content -replace 'versionName "[^"]*"', "versionName `"$newVersionStr`""
    $content | Set-Content $buildGradle
    Write-Host "Updated $buildGradle"
}

Write-Host "Done. Commit and tag:"
Write-Host "  git add version.txt $manifest $buildGradle"
Write-Host "  git commit -m \"chore: release v$newVersionStr\""
Write-Host "  git tag v$newVersionStr"
Write-Host "  git push origin main --tags"