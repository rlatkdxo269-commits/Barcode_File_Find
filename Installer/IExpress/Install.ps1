param(
    [switch]$Quiet
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Windows.Forms

$appDisplayName = '바코드 파일 찾기'
$appExeName = 'Barcode_File_Find.exe'
$uninstallKeyName = 'Barcode_File_Find'
$installDir = Join-Path $env:LOCALAPPDATA 'Programs\Barcode File Find'
$appPath = Join-Path $installDir $appExeName
$sourceAppPath = Join-Path $PSScriptRoot $appExeName

Get-Process -Name 'Barcode_File_Find' -ErrorAction SilentlyContinue | Stop-Process -Force

New-Item -ItemType Directory -Path $installDir -Force | Out-Null
Copy-Item -LiteralPath $sourceAppPath -Destination $appPath -Force

$programsDir = [Environment]::GetFolderPath('Programs')
$shortcutDir = Join-Path $programsDir $appDisplayName
$shortcutPath = Join-Path $shortcutDir "$appDisplayName.lnk"

New-Item -ItemType Directory -Path $shortcutDir -Force | Out-Null

$shell = New-Object -ComObject WScript.Shell
$shortcut = $shell.CreateShortcut($shortcutPath)
$shortcut.TargetPath = $appPath
$shortcut.WorkingDirectory = $installDir
$shortcut.Description = '바코드/QR 스캔 값으로 EPS 파일을 찾아 Illustrator에서 여는 프로그램'
$shortcut.IconLocation = $appPath
$shortcut.Save()

$uninstallScriptPath = Join-Path $installDir 'Uninstall.ps1'
$uninstallScript = @"
`$ErrorActionPreference = 'SilentlyContinue'
Get-Process -Name 'Barcode_File_Find' | Stop-Process -Force
Remove-Item -LiteralPath '$shortcutDir' -Recurse -Force
Remove-Item -LiteralPath 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\$uninstallKeyName' -Recurse -Force
Start-Process -FilePath 'cmd.exe' -ArgumentList '/c timeout /t 1 > nul & rmdir /s /q "$installDir"' -WindowStyle Hidden
"@
Set-Content -LiteralPath $uninstallScriptPath -Value $uninstallScript -Encoding UTF8

$registryPath = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\$uninstallKeyName"
New-Item -Path $registryPath -Force | Out-Null
Set-ItemProperty -Path $registryPath -Name DisplayName -Value $appDisplayName
Set-ItemProperty -Path $registryPath -Name DisplayVersion -Value '1.0.0'
Set-ItemProperty -Path $registryPath -Name Publisher -Value 'rlatkdxo269-commits'
Set-ItemProperty -Path $registryPath -Name InstallLocation -Value $installDir
Set-ItemProperty -Path $registryPath -Name DisplayIcon -Value $appPath
Set-ItemProperty -Path $registryPath -Name UninstallString -Value "powershell.exe -ExecutionPolicy Bypass -File `"$uninstallScriptPath`""
Set-ItemProperty -Path $registryPath -Name QuietUninstallString -Value "powershell.exe -ExecutionPolicy Bypass -File `"$uninstallScriptPath`""
Set-ItemProperty -Path $registryPath -Name NoModify -Value 1 -Type DWord
Set-ItemProperty -Path $registryPath -Name NoRepair -Value 1 -Type DWord

if (-not $Quiet) {
    [System.Windows.Forms.MessageBox]::Show(
        "설치가 완료되었습니다.`n`n시작 메뉴에서 '$appDisplayName'로 검색해 실행할 수 있습니다.",
        $appDisplayName,
        [System.Windows.Forms.MessageBoxButtons]::OK,
        [System.Windows.Forms.MessageBoxIcon]::Information
    ) | Out-Null
}
