param([string]$Version='1.2.1')
$ErrorActionPreference='Stop'
if($Version -notmatch '^\d+\.\d+\.\d+$') { throw 'Invalid package version.' }
& (Join-Path $PSScriptRoot 'Build.ps1') -Test
$dist=Join-Path $PSScriptRoot 'dist'
New-Item -ItemType Directory -Path $dist -Force | Out-Null
$files=@((Join-Path $PSScriptRoot 'bin\AntigravityRussian.exe'),(Join-Path $PSScriptRoot 'bin\AntigravityRussian.exe.config'),(Join-Path $PSScriptRoot 'README.md'),(Join-Path $PSScriptRoot 'LICENSE'),(Join-Path $PSScriptRoot 'CHANGELOG.md'),(Join-Path $PSScriptRoot 'docs\QUICKSTART.md'))
# The ZIP keeps the in-app updater format (EXE + config) that earlier releases expect.
$zip=Join-Path $dist ('AntigravityRussian-'+$Version+'-win-x64.zip')
Compress-Archive -LiteralPath $files -DestinationPath $zip -Force
# The standalone EXE is what people download: it carries its runtime settings inside.
$exe=Join-Path $dist ('AntigravityRussian-'+$Version+'-win-x64.exe')
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'bin\AntigravityRussian.exe') -Destination $exe -Force
foreach($file in @($exe,$zip)) {
    $hash=(Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash.ToLowerInvariant()
    [IO.File]::WriteAllText(($file+'.sha256'),$hash+'  '+[IO.Path]::GetFileName($file)+[Environment]::NewLine,(New-Object Text.UTF8Encoding($false)))
    Write-Output ('Package: '+$file+'  sha256 '+$hash)
}
