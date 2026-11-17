# 配布ビルド機（Windows）で、指定コミットの Windows 版と Mac 版を batchmode の Unity で焼く。Mac 側の lib/release-windows-bake.sh が起動する
# 引数は base64 JSON（commit・masterPin・privatePin・label・branch・targets・jobDir）。結果は jobDir の result.json、途中経過は progress.txt
# worktree は release-bake 1 本を使い回し（Library を持ち越すため）、同時に 2 本は焼かない（bake.lock を排他で握る）
# Bakes the Windows and Mac players of a commit with batchmode Unity on the Windows build machine; started by lib/release-windows-bake.sh on the Mac
# Args arrive as base64 JSON (commit, masterPin, privatePin, label, branch, targets, jobDir); the outcome is jobDir/result.json, progress goes to progress.txt
# A single release-bake worktree is reused to keep its Library, and two bakes never run at once (bake.lock is held exclusively)
param([Parameter(Mandatory=$true)][string]$ArgsB64)
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
$a = [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($ArgsB64)) | ConvertFrom-Json

$Root = 'C:\dev\worktrees\moorestech'
$Worktree = Join-Path $Root 'release-bake'
$MasterClone = Join-Path $Root 'moorestech_master'
$MasterWorktree = Join-Path $Root "_release\master-$($a.masterPin.Substring(0,8))"
$SeedPrivate = 'C:\dev\moorestech\moorestech_client\Assets\PersonalAssets\moorestech-client-private'
$SeedLibrary = 'C:\dev\moorestech\moorestech_client\Library'
$UnityExe = 'C:\Program Files\Unity\Hub\Editor\6000.3.8f1\Editor\Unity.exe'
$Rcodesign = 'C:\dev\tools\rcodesign\apple-codesign-0.29.0-x86_64-pc-windows-msvc\rcodesign.exe'
$JobDir = $a.jobDir
New-Item -ItemType Directory -Force -Path $JobDir | Out-Null

function Write-Progress2([string]$m) { "$(Get-Date -Format s) $m" | Add-Content (Join-Path $JobDir 'progress.txt') }

function Invoke-Git([string]$dir, [string[]]$gitArgs) {
  # PS5.1 は Stop 下で native の stderr を例外にするため、この関数内だけ Continue にして終了コードで判定する
  # PS5.1 turns native stderr into exceptions under Stop, so judge by exit code with Continue here
  $ErrorActionPreference = 'Continue'
  $out = & git -C $dir @gitArgs 2>&1
  if ($LASTEXITCODE -ne 0) { throw "git $($gitArgs -join ' ') @ $dir failed: $out" }
  return $out
}

# 手元に無いコミットだけ GitHub から取ってから detach する
# Fetch from GitHub only when the commit is missing, then detach to it
function Set-CheckoutToCommit([string]$dir, [string]$commit) {
  $ErrorActionPreference = 'Continue'
  & git -C $dir cat-file -e "$commit^{commit}" 2>$null
  if ($LASTEXITCODE -ne 0) { Invoke-Git $dir @('fetch','-q','origin') | Out-Null }
  Invoke-Git $dir @('checkout','-q','--detach','-f',$commit) | Out-Null
  Invoke-Git $dir @('clean','-fdq') | Out-Null
  if ((Invoke-Git $dir @('rev-parse','HEAD')).Trim() -ne $commit) { throw "HEAD of $dir is not $commit" }
}

# 焼く worktree・同梱元 master・非公開アセット・Library をピンへ揃える
# Align the baking worktree, bundled master, private assets and Library to the pins
function Initialize-BakeCheckout {
  $repo = Join-Path $Root '_repo'
  if (-not (Test-Path (Join-Path $Worktree '.git'))) {
    $ErrorActionPreference = 'Continue'
    & git -C $repo cat-file -e "$($a.commit)^{commit}" 2>$null
    if ($LASTEXITCODE -ne 0) { Invoke-Git $repo @('fetch','-q','origin') | Out-Null }
    Invoke-Git $repo @('worktree','add','-q','--detach',$Worktree,$a.commit) | Out-Null
    $ErrorActionPreference = 'Stop'
  }
  Set-CheckoutToCommit $Worktree $a.commit
  if (-not (Test-Path (Join-Path $MasterWorktree '.git'))) {
    $ErrorActionPreference = 'Continue'
    & git -C $MasterClone cat-file -e "$($a.masterPin)^{commit}" 2>$null
    if ($LASTEXITCODE -ne 0) { Invoke-Git $MasterClone @('fetch','-q','origin') | Out-Null }
    Invoke-Git $MasterClone @('worktree','add','-q','--detach',$MasterWorktree,$a.masterPin) | Out-Null
    $ErrorActionPreference = 'Stop'
  }
  Set-CheckoutToCommit $MasterWorktree $a.masterPin
  $private = Join-Path $Worktree 'moorestech_client\Assets\PersonalAssets\moorestech-client-private'
  if (-not (Test-Path (Join-Path $private '.git'))) {
    $ErrorActionPreference = 'Continue'
    & git clone -q --no-checkout $SeedPrivate $private 2>&1 | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "private clone failed: $private" }
    $ErrorActionPreference = 'Stop'
    Invoke-Git $private @('remote','set-url','origin','git@github.com:moorestech/moorestech-client-private.git') | Out-Null
  }
  Set-CheckoutToCommit $private $a.privatePin
  # LFS の実体が来ていない ffmpeg は焼いても録画が動かないので、焼く前に止める
  # An ffmpeg left as an LFS pointer would ship broken recording, so stop before baking
  foreach ($rel in 'ffmpeg\win-x64\ffmpeg.exe','ffmpeg\macos-arm64\ffmpeg') {
    $f = Get-Item (Join-Path $private $rel)
    if ($f.Length -le 1024) { throw "ffmpeg is an LFS pointer: $($f.FullName)" }
  }
  $lib = Join-Path $Worktree 'moorestech_client\Library'
  if (-not (Test-Path $lib)) {
    # Library の複製は 1 本約 80GB。空きが足りないまま複製すると並走中のジョブまで落とすので先に止める
    # A Library copy is ~80GB; copying without room breaks parallel jobs too, so stop here first
    $freeGB = [math]::Floor((Get-PSDrive ($Worktree.Substring(0,1))).Free / 1GB)
    if ($freeGB -lt 150) { throw "ディスクの空きが ${freeGB}GB しかない（Library 複製に 150GB 要る）" }
    Write-Progress2 "copying Library from $SeedLibrary"
    & robocopy $SeedLibrary $lib /E /MT:16 /R:0 /W:0 /NFL /NDL /NJH /NJS /NP | Out-Null
    if ($LASTEXITCODE -ge 8) { throw "robocopy Library failed (code $LASTEXITCODE)" }
  }
}

# 1 ターゲットを焼く。環境変数は Mac で焼くときと同じ契約（SteamPlaytestBuildCli・BuildInfoWriter・GameDataBundler が読む）
# Bake one target; the env vars follow the same contract as a Mac bake (read by SteamPlaytestBuildCli, BuildInfoWriter and GameDataBundler)
function Invoke-BakeTarget([string]$target) {
  $method = @{ StandaloneWindows64 = 'WindowsSteamPlaytestBuild'; StandaloneOSX = 'MacOsSteamPlaytestBuild' }[$target]
  $dirName = @{ StandaloneWindows64 = 'build-windows'; StandaloneOSX = 'build-mac' }[$target]
  if (-not $method) { throw "unknown target: $target" }
  $out = Join-Path $JobDir $dirName
  if (Test-Path $out) { Remove-Item -Recurse -Force $out }
  New-Item -ItemType Directory -Force -Path $out | Out-Null
  $log = Join-Path $JobDir "unity-$dirName.log"
  $env:MOORESTECH_BUILD_OUTPUT = $out
  $env:MOORESTECH_STEAM_BUILD_LABEL = $a.label
  $env:MOORESTECH_BUILD_BRANCH = $a.branch
  $env:MOORESTECH_MASTER_DATA_ROOT = $MasterWorktree
  $env:MOORESTECH_RCODESIGN_PATH = $Rcodesign
  $argv = "-batchmode -nographics -projectPath `"$Worktree\moorestech_client`" -executeMethod Client.Editor.Build.SteamPlaytestBuildCli.$method -logFile `"$log`""
  Write-Progress2 "unity start $target"
  $t0 = Get-Date
  $p = Start-Process -FilePath $UnityExe -ArgumentList $argv -PassThru -WindowStyle Hidden
  $p.Id | Set-Content (Join-Path $JobDir "unity-$dirName.pid")
  $p.WaitForExit()
  $seconds = [int]((Get-Date) - $t0).TotalSeconds
  Write-Progress2 "unity exit $target code=$($p.ExitCode) seconds=$seconds"
  return [pscustomobject]@{ target = $target; dir = $dirName; exitCode = $p.ExitCode; seconds = $seconds }
}

$result = [ordered]@{ ok = $false; commit = $a.commit; label = $a.label; targets = @(); error = $null }
$lockStream = $null
try {
  # 他の配布ビルドが握っている間は焼かない（同じ worktree と Library を同時に触らせない）
  # Refuse while another release bake holds the lock, so the shared worktree and Library are never touched twice
  New-Item -ItemType Directory -Force -Path (Join-Path $Root '_release') | Out-Null
  $lockStream = [IO.File]::Open((Join-Path $Root '_release\bake.lock'), 'OpenOrCreate', 'ReadWrite', 'None')
  Write-Progress2 "prepare $($a.commit)"
  Initialize-BakeCheckout
  Write-Progress2 'prepare done'
  foreach ($target in $a.targets) {
    $r = Invoke-BakeTarget $target
    $result.targets += $r
    if ($r.exitCode -ne 0) { throw "Unity が $target で終了コード $($r.exitCode) を返した（$($JobDir)\unity-$($r.dir).log）" }
  }
  $result.ok = $true
} catch {
  # 失敗は result.json と progress.txt の両方に残し、Mac 側の待ち手が理由ごと受け取る
  # Failures land in both result.json and progress.txt so the waiting Mac side receives the reason
  $result.error = "$_"
  Write-Progress2 "FAILED $_"
} finally {
  if ($lockStream) { $lockStream.Dispose() }
  [pscustomobject]$result | ConvertTo-Json -Depth 5 | Set-Content -Encoding UTF8 (Join-Path $JobDir 'result.json')
}
