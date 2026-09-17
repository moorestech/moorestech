# 検証機で playtest ブランチの更新を待ち、配布ビルドの通し検証（phase1/phase2）を Steam 経由で実行する
# SteamUser 引数は使わない。Steam はテスター用アカウントで事前ログイン済み前提（README 手順6）
# 終了コード: 2=ラベル更新待ちの期限切れ 3=ゲーム未インストール 4=result.json 無し 5=検証失敗 6=フェーズ期限切れ 7=steam.exe 不在
# Waits for the playtest branch update on the check machine and runs the distribution smoke (phase1/phase2) through Steam
# No SteamUser parameter: Steam is expected to be pre-logged-in with the tester account (README step 6)
# Exit codes: 2=label wait expired 3=game not installed 4=no result.json 5=smoke failed 6=phase timed out 7=steam.exe missing
param(
    [Parameter(Mandatory = $true)][string]$ResultRoot,
    [Parameter(Mandatory = $true)][string]$ExpectedBuildLabel
)

$ErrorActionPreference = "Stop"
$AppId = "1958160"
$GameProcessName = "moorestech"
$PhaseTimeoutSeconds = 600

# Stop 下の Write-Error は終了エラーになり exit に届かない。理由を標準エラーへ直接書いてから意図した終了コードで抜ける
# Under Stop, Write-Error is terminating and never reaches exit; write the reason to stderr directly, then exit with the intended code
function Fail([int]$Code, [string]$Message) {
    [Console]::Error.WriteLine("ERROR: $Message")
    exit $Code
}

# steam.exe は env → レジストリ → 既定の Program Files (x86) の順に探し、見つからなければ確認した場所を理由に出す
# Look for steam.exe via env, then the registry, then the default Program Files (x86); if absent, report every place checked
function Resolve-SteamExe {
    if ($env:MOORESTECH_STEAM_EXE) {
        if (Test-Path $env:MOORESTECH_STEAM_EXE) { return $env:MOORESTECH_STEAM_EXE }
        Fail 7 "MOORESTECH_STEAM_EXE が指す steam.exe が存在しません: $($env:MOORESTECH_STEAM_EXE)"
    }
    $candidates = @()
    $userSteam = Get-ItemProperty -Path "HKCU:\Software\Valve\Steam" -Name SteamExe -ErrorAction SilentlyContinue
    if ($userSteam) { $candidates += ($userSteam.SteamExe -replace "/", "\") }
    $machineSteam = Get-ItemProperty -Path "HKLM:\SOFTWARE\WOW6432Node\Valve\Steam" -Name InstallPath -ErrorAction SilentlyContinue
    if ($machineSteam) { $candidates += (Join-Path $machineSteam.InstallPath "steam.exe") }
    $candidates += (Join-Path ${env:ProgramFiles(x86)} "Steam\steam.exe")
    foreach ($candidate in $candidates) {
        if (Test-Path $candidate) { return $candidate }
    }
    Fail 7 "steam.exe が見つかりません（確認した場所: $($candidates -join ', ')）。MOORESTECH_STEAM_EXE で指定してください"
}

# 前のフェーズや手動起動のゲームが残っていると Steam が新しい起動を拒否・更新を保留するため、起動前に必ず畳む
# A leftover game from a previous phase or a manual launch makes Steam refuse the new launch or hold the update, so close it first
function Stop-LeftoverGame([string]$Reason) {
    $leftover = Get-Process -Name $GameProcessName -ErrorAction SilentlyContinue
    if ($leftover) {
        Write-Output "stopping leftover $GameProcessName ($Reason): pid=$($leftover.Id -join ',')"
        $leftover | Stop-Process -Force
    }
}

$SteamExe = Resolve-SteamExe
$GameDirectory = Join-Path (Split-Path $SteamExe -Parent) "steamapps\common\moorestech"
$GameExe = Join-Path $GameDirectory "moorestech.exe"
$BuildInfoPath = Join-Path $GameDirectory "moorestech_Data\StreamingAssets\build-info.json"
Write-Output "steam.exe: $SteamExe"

if (Test-Path $ResultRoot) { Remove-Item $ResultRoot -Recurse -Force }
New-Item -ItemType Directory -Path $ResultRoot | Out-Null

# ゲームを起動せずに Steam を常駐させ、playtest ブランチの自動更新で build-info.json のラベルが切り替わるまで待つ
# （ゲームが動いていると更新が保留されるので先に畳む。exe の更新時刻は差分更新で変わらないことがあるため使わない）
# Keep Steam running without launching the game and wait until the playtest branch auto-update switches build-info.json's label
# (a running game holds the update, so close it first; the exe timestamp can be unchanged on a partial update)
Stop-LeftoverGame "before the update wait"
Start-Process -FilePath $SteamExe -ArgumentList @("-silent")

$deadline = (Get-Date).AddMinutes(20)
$matched = $false
$lastReadError = "build-info.json がまだ存在しない"
$lastSeenLabel = "(none)"
while ((Get-Date) -lt $deadline) {
    if (Test-Path $BuildInfoPath) {
        try {
            $buildInfo = Get-Content $BuildInfoPath -Raw | ConvertFrom-Json
            $lastSeenLabel = "$($buildInfo.steamBuildLabel)"
            if ($buildInfo.steamBuildLabel -eq $ExpectedBuildLabel) { $matched = $true; break }
        } catch {
            # 更新中の断片 JSON なら次のポーリングで読める。恒常的な失敗（権限・破損）は最後の理由として期限切れ時に出す
            # A mid-write fragment reads fine on the next poll; a persistent failure (permission, corruption) surfaces in the expiry message
            $lastReadError = $_.Exception.Message
        }
    }
    Start-Sleep -Seconds 15
}
if (-not $matched) {
    Fail 2 "期限内に steamBuildLabel=$ExpectedBuildLabel へ更新されませんでした（最後に読めたラベル: $lastSeenLabel、最後の読み取り失敗: $lastReadError。Steam のダウンロード状況を確認すること）: $BuildInfoPath"
}
if (-not (Test-Path $GameExe)) {
    Fail 3 "moorestech.exe が見つかりません: $GameExe"
}

# テスターと同じ起動経路（Steam の DRM・AppID 決定）を通すため、smoke 引数ごと steam.exe -applaunch で起動する
# Steam 経由ではゲームのプロセスハンドルも終了コードも得られない。合否は result.json の success だけで判定する
# Launch through steam.exe -applaunch with the smoke arguments so the tester's launch path (Steam DRM, AppID) is exercised
# Launching via Steam yields neither a process handle nor an exit code; the verdict comes from result.json's success alone
foreach ($phase in @("phase1", "phase2")) {
    $phaseDirectory = Join-Path $ResultRoot $phase
    $resultPath = Join-Path $phaseDirectory "result.json"
    New-Item -ItemType Directory -Path $phaseDirectory | Out-Null
    Stop-LeftoverGame "before $phase"
    Start-Process -FilePath $SteamExe -ArgumentList @("-applaunch", $AppId, "--playtestSmoke", "--smokePhase", $phase, "--smokeResultDirectory", $phaseDirectory)

    # result.json が出て、かつゲームが自分で終了するまで待つ。書きかけの読み取りと、次フェーズ起動時の二重起動を避けるため
    # Wait until result.json exists AND the game has quit by itself, avoiding a half-written read and a double launch in the next phase
    $phaseDeadline = (Get-Date).AddSeconds($PhaseTimeoutSeconds)
    $finished = $false
    while ((Get-Date) -lt $phaseDeadline) {
        $running = Get-Process -Name $GameProcessName -ErrorAction SilentlyContinue
        if ((Test-Path $resultPath) -and -not $running) { $finished = $true; break }
        Start-Sleep -Seconds 5
    }

    # 期限切れはクラッシュ確認画面・起動引数の確認ダイアログ・Steam の起動拒否などで止まっている。次フェーズを汚さないよう畳んで失敗にする
    # An overrun means a crash dialog, a launch-argument prompt or a refused launch; close the game so the next phase is not tainted, then fail
    if (-not $finished) {
        Stop-LeftoverGame "$phase timed out"
        $state = if (Test-Path $resultPath) { "result.json は出たがゲームが終了しなかった" } else { "result.json が出なかった" }
        Fail 6 "$phase が ${PhaseTimeoutSeconds}秒以内に終わりませんでした（$state。クラッシュ確認画面・起動引数の確認ダイアログ・Steam の起動拒否の可能性）。ゲームを強制終了しました"
    }
    if (-not (Test-Path $resultPath)) {
        Fail 4 "$phase が result.json を書きませんでした: $resultPath"
    }

    # トップレベルの success だけを見る。ステップ配列にも同名キーがあるため、全文正規表現ではどちらか一方が true なだけで合格にしてしまう
    # Read only the top-level success; the steps array carries the same key name, so a whole-text regex would pass when either one alone is true
    $resultJson = Get-Content $resultPath -Raw | ConvertFrom-Json
    if ($resultJson.success -ne $true) {
        Fail 5 "$phase の通し検証が失敗しました: $(Get-Content $resultPath -Raw)"
    }
    Write-Output "$phase passed"
}

Write-Output "smoke completed"
