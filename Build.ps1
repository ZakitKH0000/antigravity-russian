param([switch]$Test)
$ErrorActionPreference='Stop'
$root=$PSScriptRoot
& (Join-Path $root 'Sync-Payload.ps1')
$compiler=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if(-not (Test-Path -LiteralPath $compiler)) { throw '.NET Framework 4.8 / Windows x64 is required.' }
$bin=Join-Path $root 'bin'
New-Item -ItemType Directory -Path $bin -Force | Out-Null
$output=Join-Path $bin 'AntigravityRussian.exe'
$sources=@(Get-ChildItem -LiteralPath (Join-Path $root 'src') -Filter '*.cs' | ForEach-Object FullName)
$arguments=@('/nologo','/target:winexe','/platform:x64','/optimize+','/langversion:5','/utf8output',('/out:'+$output),('/win32manifest:'+(Join-Path $root 'assets\app.manifest')),('/win32icon:'+(Join-Path $root 'assets\app.ico')),
    '/reference:System.dll','/reference:System.Core.dll','/reference:System.Drawing.dll','/reference:System.Windows.Forms.dll','/reference:System.Web.Extensions.dll')
foreach($name in @('localize-ui.js','dictionary.json','native-rules.json')) { $arguments+=('/resource:'+(Join-Path $root ('payload\'+$name))+',payload.'+$name) }
& $compiler @arguments @sources
if($LASTEXITCODE -ne 0) { throw 'Application build failed.' }
Copy-Item -LiteralPath (Join-Path $root 'assets\App.config') -Destination ($output+'.config') -Force
if($Test) {
    $testExe=Join-Path $bin 'Tests.exe'
    & $compiler /nologo /target:exe /platform:x64 /langversion:5 /utf8output ('/out:'+$testExe) ('/reference:'+$output) /reference:System.Core.dll /reference:System.Web.Extensions.dll (Join-Path $root 'tests\Tests.cs')
    if($LASTEXITCODE -ne 0) { throw 'Test build failed.' }
    & $testExe
    if($LASTEXITCODE -ne 0) { throw 'Tests failed.' }
}
Write-Output ('Built: '+$output)
