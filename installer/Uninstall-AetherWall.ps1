param([switch]$Cleanup, [string]$InstallDirectoryOverride)

$ErrorActionPreference = 'SilentlyContinue'
$installDirectory = if ([string]::IsNullOrWhiteSpace($InstallDirectoryOverride)) { $PSScriptRoot } else { $InstallDirectoryOverride }
$startMenuDirectory = Join-Path ([Environment]::GetFolderPath('Programs')) 'AetherWall'
$desktopShortcut = Join-Path ([Environment]::GetFolderPath('Desktop')) 'AetherWall.lnk'
$uninstallRegistryPath = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\AetherWall'

if ($Cleanup) {
    Start-Sleep -Seconds 2
    Remove-Item -LiteralPath $desktopShortcut -Force
    Remove-Item -LiteralPath $startMenuDirectory -Recurse -Force
    Remove-Item -LiteralPath $uninstallRegistryPath -Recurse -Force
    Remove-Item -LiteralPath $installDirectory -Recurse -Force
    Remove-Item -LiteralPath $PSCommandPath -Force
    exit 0
}

Add-Type -AssemblyName System.Windows.Forms
$confirmation = [System.Windows.Forms.MessageBox]::Show(
    'Remove AetherWall and its shortcuts? Your imported wallpapers and settings will be kept.',
    'Uninstall AetherWall',
    [System.Windows.Forms.MessageBoxButtons]::YesNo,
    [System.Windows.Forms.MessageBoxIcon]::Question)
if ($confirmation -ne [System.Windows.Forms.DialogResult]::Yes) { exit 0 }

Get-Process -Name 'AetherWall' -ErrorAction SilentlyContinue | Stop-Process -Force
$cleanupScript = Join-Path $env:TEMP ('AetherWall-Uninstall-' + [guid]::NewGuid().ToString('N') + '.ps1')
Copy-Item -LiteralPath $PSCommandPath -Destination $cleanupScript -Force
$arguments = '-NoProfile -ExecutionPolicy Bypass -File "' + $cleanupScript + '" -Cleanup -InstallDirectoryOverride "' + $installDirectory + '"'
Start-Process -FilePath 'powershell.exe' -ArgumentList $arguments -WindowStyle Hidden
