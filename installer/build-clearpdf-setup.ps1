$ErrorActionPreference = 'Stop'
$InstallerDir = 'C:\Users\Steve\Documents\ClearPDF\installer'
$Docs = 'C:\Users\Steve\Documents'
$Desktop = [Environment]::GetFolderPath('Desktop')
$Apps = 'C:\Users\Steve\Documents\Apps\ClearPDF'
$Publish = 'C:\ClearPDF-publish\win-x64'

# Prefer Apps if ClearPDF.exe present
if ((Test-Path (Join-Path $Apps 'ClearPDF.exe'))) {
  $AppSource = $Apps
} elseif ((Test-Path (Join-Path $Publish 'ClearPDF.exe'))) {
  $AppSource = $Publish
} else {
  throw "No ClearPDF.exe in Apps or publish folder"
}

Write-Host "AppSource=$AppSource"
New-Item -ItemType Directory -Force -Path $InstallerDir | Out-Null

# Find or install Inno Setup 6
$IsccCandidates = @(
  "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
  "$env:ProgramFiles\Inno Setup 6\ISCC.exe",
  'C:\Program Files (x86)\Inno Setup 6\ISCC.exe',
  'C:\Program Files\Inno Setup 6\ISCC.exe',
  "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe",
  'C:\Users\Steve\AppData\Local\Programs\Inno Setup 6\ISCC.exe'
)
$Iscc = $IsccCandidates | Where-Object { Test-Path $_ } | Select-Object -First 1

if (-not $Iscc) {
  Write-Host 'Installing Inno Setup 6 silently...'
  $InnoInstaller = Join-Path $env:TEMP 'innosetup-6-install.exe'
  $urls = @(
    'https://github.com/jrsoftware/issrc/releases/download/is-6_7_3/innosetup-6.7.3.exe',
    'https://jrsoftware.org/download.php/is.exe'
  )
  $downloaded = $false
  foreach ($u in $urls) {
    try {
      Invoke-WebRequest -Uri $u -OutFile $InnoInstaller -UseBasicParsing -TimeoutSec 120
      if ((Get-Item $InnoInstaller).Length -gt 1MB) { $downloaded = $true; break }
    } catch {
      Write-Host "Download failed: $u :: $_"
    }
  }
  if (-not $downloaded) { throw 'Failed to download Inno Setup installer' }
  Start-Process -FilePath $InnoInstaller -ArgumentList '/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART' -Wait -NoNewWindow
  Start-Sleep -Seconds 2
  $Iscc = $IsccCandidates | Where-Object { Test-Path $_ } | Select-Object -First 1
  if (-not $Iscc) { throw 'ISCC.exe not found after Inno install' }
}

Write-Host "ISCC=$Iscc"

# Convert 55png to BMP if small bmp missing
$SmallBmp = Join-Path $InstallerDir 'inno-wizard-small-55x55.bmp'
$SmallPng = Join-Path $InstallerDir 'inno-icon-55.png'
if (-not (Test-Path $SmallBmp) -and (Test-Path $SmallPng)) {
  Add-Type -AssemblyName System.Drawing
  $img = [System.Drawing.Image]::FromFile($SmallPng)
  $bmp = New-Object System.Drawing.Bitmap $img, 55, 55
  $bmp.Save($SmallBmp, [System.Drawing.Imaging.ImageFormat]::Bmp)
  $bmp.Dispose(); $img.Dispose()
  Write-Host "Created $SmallBmp"
}

$Iss = Join-Path $InstallerDir 'ClearPDF.iss'
if (-not (Test-Path $Iss)) { throw "Missing $Iss" }

# Compile
& $Iscc "/DAppSource=$AppSource" "/DSetupOutputDir=$Docs" "/DInstallerDir=$InstallerDir" $Iss
if ($LASTEXITCODE -ne 0) { throw "ISCC failed with exit $LASTEXITCODE" }

$Setup = Join-Path $Docs 'ClearPDF-Setup.exe'
if (-not (Test-Path $Setup)) { throw "Setup not produced at $Setup" }

Copy-Item -Force $Setup (Join-Path $Desktop 'ClearPDF-Setup.exe')
$item = Get-Item $Setup
$mb = [math]::Round($item.Length / 1MB, 2)
Write-Host "SUCCESS"
Write-Host "PATH=$($item.FullName)"
Write-Host "DESKTOP=$(Join-Path $Desktop 'ClearPDF-Setup.exe')"
Write-Host "SIZE_BYTES=$($item.Length)"
Write-Host "SIZE_MB=$mb"
