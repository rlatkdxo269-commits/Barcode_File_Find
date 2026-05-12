param(
    [string]$Configuration = 'Release',
    [string]$Runtime = 'win-x64'
)

$ErrorActionPreference = 'Stop'

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..\..')
$publishDir = Join-Path $repoRoot 'publish-single'
$packageDir = Join-Path $repoRoot 'installer-iexpress\package'
$outputDir = Join-Path $repoRoot 'installer-iexpress'
$setupPath = Join-Path $outputDir 'Barcode_File_Find_Setup.exe'
$sedPath = Join-Path $outputDir 'Barcode_File_Find_Setup.sed'

dotnet publish $repoRoot `
    -c $Configuration `
    -r $Runtime `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true `
    -o $publishDir

New-Item -ItemType Directory -Path $packageDir -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $publishDir 'Barcode_File_Find.exe') -Destination (Join-Path $packageDir 'Barcode_File_Find.exe') -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Install.ps1') -Destination (Join-Path $packageDir 'Install.ps1') -Force

$escapedSetupPath = $setupPath.Replace('\', '\\')
$escapedPackageDir = $packageDir.Replace('\', '\\')

$sed = @"
[Version]
Class=IEXPRESS
SEDVersion=3

[Options]
PackagePurpose=InstallApp
ShowInstallProgramWindow=0
HideExtractAnimation=1
UseLongFileName=1
InsideCompressed=0
CAB_FixedSize=0
CAB_ResvCodeSigning=0
RebootMode=N
InstallPrompt=
DisplayLicense=
FinishMessage=
TargetName=$escapedSetupPath
FriendlyName=Barcode File Find Setup
AppLaunched=powershell.exe -NoProfile -ExecutionPolicy Bypass -File Install.ps1
PostInstallCmd=<None>
AdminQuietInstCmd=powershell.exe -NoProfile -ExecutionPolicy Bypass -File Install.ps1 -Quiet
UserQuietInstCmd=powershell.exe -NoProfile -ExecutionPolicy Bypass -File Install.ps1 -Quiet
SourceFiles=SourceFiles

[Strings]
FILE0="Barcode_File_Find.exe"
FILE1="Install.ps1"

[SourceFiles]
SourceFiles0=$escapedPackageDir

[SourceFiles0]
%FILE0%=
%FILE1%=
"@

New-Item -ItemType Directory -Path $outputDir -Force | Out-Null
Set-Content -LiteralPath $sedPath -Value $sed -Encoding Default
& "$env:WINDIR\System32\iexpress.exe" /N /Q $sedPath

if (-not (Test-Path -LiteralPath $setupPath)) {
    throw "IExpress did not create $setupPath"
}

Get-Item -LiteralPath $setupPath
exit 0
