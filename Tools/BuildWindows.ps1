param([string]$UnityPath)
$ErrorActionPreference='Stop'
$projectRoot=Split-Path -Parent $PSScriptRoot
if([string]::IsNullOrWhiteSpace($UnityPath)) {
    $UnityPath=Join-Path ${env:ProgramFiles} 'Unity/Hub/Editor/6000.3.24f1/Editor/Unity.exe'
}
if(!(Test-Path -LiteralPath $UnityPath)) { throw 'Unity 6000.3.24f1 was not found. Pass -UnityPath with the full path to Unity.exe.' }
$logsPath=Join-Path $projectRoot 'Logs'
New-Item -ItemType Directory -Path $logsPath -Force | Out-Null
$logPath=Join-Path $logsPath 'Windows-build.log'
$arguments=@('-batchmode','-nographics','-quit','-projectPath',$projectRoot,'-executeMethod','AerBuilder.BuildWindows','-logFile',$logPath)
$process=Start-Process -FilePath $UnityPath -ArgumentList ($arguments|ForEach-Object{'"'+$_+'"'}) -WindowStyle Hidden -PassThru
$process.WaitForExit()
$log=Get-Content -LiteralPath $logPath -Raw
if($process.ExitCode -ne 0 -or $log -notmatch 'AER_BUILD_SUCCEEDED:') { throw "Build failed. See $logPath" }
Write-Output (Join-Path $projectRoot 'Builds/ProtocolAer.exe')
