param([string]$Version='1.1.0')
$ErrorActionPreference='Stop'
if($Version -notmatch '^\d+\.\d+\.\d+$') { throw 'Invalid package version.' }
& (Join-Path $PSScriptRoot 'Build.ps1') -Test
$dist=Join-Path $PSScriptRoot 'dist'
New-Item -ItemType Directory -Path $dist -Force | Out-Null
$files=@((Join-Path $PSScriptRoot 'bin\AntigravityRussian.exe'),(Join-Path $PSScriptRoot 'bin\AntigravityRussian.exe.config'),(Join-Path $PSScriptRoot 'README.md'),(Join-Path $PSScriptRoot 'LICENSE'),(Join-Path $PSScriptRoot 'CHANGELOG.md'),(Join-Path $PSScriptRoot 'docs\QUICKSTART.md'))
$zip=Join-Path $dist ('AntigravityRussian-'+$Version+'-win-x64.zip')
Compress-Archive -LiteralPath $files -DestinationPath $zip -Force
$hash=(Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash.ToLowerInvariant()
[IO.File]::WriteAllText(($zip+'.sha256'),$hash+'  '+[IO.Path]::GetFileName($zip)+[Environment]::NewLine,(New-Object Text.UTF8Encoding($false)))
Write-Output ('Package: '+$zip)
