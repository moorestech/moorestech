# moorestech_web セットアップ: Node.js LTS と pnpm スタンドアロンバイナリを
# moorestech_web/node/win-x64/ にダウンロードする。
# Setup for moorestech_web: downloads Node.js LTS and pnpm standalone
# binaries into moorestech_web/node/win-x64/.
$ErrorActionPreference = "Stop"
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Definition
$NodeVersion = "20.18.1"
$PnpmVersion = "9.15.0"
$Platform = "win-x64"
$TargetDir = Join-Path $ScriptDir "node\$Platform"
New-Item -ItemType Directory -Force -Path $TargetDir | Out-Null

$NodeUrl = "https://nodejs.org/dist/v$NodeVersion/node-v$NodeVersion-win-x64.zip"
$PnpmUrl = "https://github.com/pnpm/pnpm/releases/download/v$PnpmVersion/pnpm-win-x64.exe"

Write-Host "[setup] downloading node from $NodeUrl"
$NodeZip = Join-Path $env:TEMP "node.zip"
Invoke-WebRequest -Uri $NodeUrl -OutFile $NodeZip -UseBasicParsing
Expand-Archive -Path $NodeZip -DestinationPath $TargetDir -Force
# zip 内は node-v20.18.1-win-x64/ 配下なので 1階層下げる
# The zip contains a top-level node-v20.18.1-win-x64/ dir, flatten it
$Inner = Get-ChildItem -Path $TargetDir -Directory | Select-Object -First 1
Move-Item -Path (Join-Path $Inner.FullName "*") -Destination $TargetDir -Force
Remove-Item -Path $Inner.FullName -Recurse -Force
Remove-Item $NodeZip

Write-Host "[setup] downloading pnpm from $PnpmUrl"
Invoke-WebRequest -Uri $PnpmUrl -OutFile (Join-Path $TargetDir "pnpm.exe") -UseBasicParsing

Write-Host "[setup] done. node: $TargetDir\node.exe, pnpm: $TargetDir\pnpm.exe"
