param([string]$Executable)
$ErrorActionPreference='Stop'
$projectRoot=Split-Path -Parent $PSScriptRoot
if([string]::IsNullOrWhiteSpace($Executable)) { $Executable=Join-Path $projectRoot 'Builds/ProtocolAer.exe' }
if(!(Test-Path -LiteralPath $Executable)) { throw 'Build the Windows player first, or pass -Executable with a full path.' }
$Executable=(Resolve-Path -LiteralPath $Executable).Path
$logsPath=Join-Path $projectRoot 'Logs'
New-Item -ItemType Directory -Path $logsPath -Force | Out-Null
$capturePath=Join-Path $logsPath 'Screenshots'
$logPath=Join-Path $logsPath 'Windows-verification.log'
$arguments=@('-screen-width','1440','-screen-height','900','-screen-fullscreen','0','-force-d3d11','-aer-capture',$capturePath,'-logFile',$logPath)
# This is an interactive player check; the window is visible to capture frames.
$process=Start-Process -FilePath $Executable -ArgumentList ($arguments|ForEach-Object{'"'+$_+'"'}) -PassThru
if(!$process.WaitForExit(120000)) { $process.Kill();throw "Verification timed out. See $logPath" }
$log=Get-Content -LiteralPath $logPath -Raw
foreach($marker in @('AER_PLACEMENT_PASSED:','AER_HINTS_PASSED:','AER_RUNTIME_PASSED:')) {
    if($log -notmatch [regex]::Escape($marker)) { throw "Missing $marker See $logPath" }
}
if($log -match 'Exception:') { throw "Runtime exception. See $logPath" }
Write-Output "Verification passed. Screenshots: $capturePath"
