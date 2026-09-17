# 検証機で playtest ブランチを更新し、配布ビルドの通し検証（phase1/phase2）を実行する
# Updates the playtest branch on the check machine and runs the distribution smoke (phase1/phase2)
# SteamUser 引数は使わない。Steam はテスター用アカウントで事前ログイン済み前提（README 手順6）
# No SteamUser parameter: Steam is expected to be pre-logged-in with the tester account (README step 6)
param(
    [Parameter(Mandatory = $true)][string]$ResultRoot,
    [Parameter(Mandatory = $true)][string]$ExpectedBuildLabel
)

$ErrorActionPreference = "Stop"
$AppId = "1958160"
$SteamExe = "C:\Program Files (x86)\Steam\steam.exe"
$GameExe = "C:\Program Files (x86)\Steam\steamapps\common\moorestech\moorestech.exe"
$BuildInfoPath = "C:\Program Files (x86)\Steam\steamapps\common\moorestech\moorestech_Data\StreamingAssets\build-info.json"

if (Test-Path $ResultRoot) { Remove-Item $ResultRoot -Recurse -Force }
New-Item -ItemType Directory -Path $ResultRoot | Out-Null

# playtest ブランチの更新を Steam クライアントへ依頼し、build-info.json の steamBuildLabel が
# 検証対象のラベルへ切り替わるまで待つ（exe の更新時刻は差分更新で変わらないことがあるため使わない）
# Ask the Steam client to update the playtest branch and wait until build-info.json's
# steamBuildLabel matches the label under test (the exe timestamp can be unchanged on a partial update)
Start-Process -FilePath $SteamExe -ArgumentList @("-applaunch", $AppId, "-silent") -Wait:$false
Start-Process -FilePath $SteamExe -ArgumentList @("+app_update", $AppId) -Wait:$false

$deadline = (Get-Date).AddMinutes(20)
$matched = $false
while ((Get-Date) -lt $deadline) {
    if (Test-Path $BuildInfoPath) {
        try {
            $buildInfo = Get-Content $BuildInfoPath -Raw | ConvertFrom-Json
            if ($buildInfo.steamBuildLabel -eq $ExpectedBuildLabel) { $matched = $true; break }
        } catch {
            # JSON が更新中の断片状態で読めることがある。次のポーリングへ回す
            # The JSON can be read mid-write as a partial fragment; fall through to the next poll
        }
    }
    Start-Sleep -Seconds 15
}
if (-not $matched) {
    Write-Error "期限内に steamBuildLabel=$ExpectedBuildLabel へ更新されませんでした: $BuildInfoPath"
    exit 2
}
if (-not (Test-Path $GameExe)) {
    Write-Error "moorestech.exe が見つかりません: $GameExe"
    exit 3
}

# フェーズごとに個別プロセスタイムアウトを持つ。phase1 がクラッシュすると次回起動時のクラッシュ
# 確認画面で phase2 が無期限に止まるため、待ちきれない場合は kill して理由付きで失敗にする
# Each phase has its own process timeout. If phase1 crashes, the next launch's crash-report
# dialog would otherwise stall phase2 forever, so an overrun is killed and failed with a reason
$PhaseTimeoutSeconds = 600
foreach ($phase in @("phase1", "phase2")) {
    $phaseDirectory = Join-Path $ResultRoot $phase
    New-Item -ItemType Directory -Path $phaseDirectory | Out-Null
    $process = Start-Process -FilePath $GameExe -PassThru `
        -ArgumentList @("--playtestSmoke", "--smokePhase", $phase, "--smokeResultDirectory", $phaseDirectory)
    $exited = $process.WaitForExit($PhaseTimeoutSeconds * 1000)
    $resultPath = Join-Path $phaseDirectory "result.json"

    if (-not $exited) {
        Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue
        Write-Error "$phase が ${PhaseTimeoutSeconds}秒以内に終了しませんでした（クラッシュ確認画面で止まっている可能性）。プロセスを強制終了しました"
        exit 6
    }

    # 終了コードは参考ログに留める。GameShutdownEvent が Application.Quit を引き留めて
    # 無引数で再 Quit するため、終了コードが失われうる。合否は result.json の success で判定する
    # The exit code is logged for reference only, not trusted for pass/fail. GameShutdownEvent
    # can defer Application.Quit and re-fire it without an argument, losing the exit code; the
    # verdict comes from result.json's success field instead
    Write-Output "$phase exit=$($process.ExitCode) (reference only; verdict comes from result.json)"

    if (-not (Test-Path $resultPath)) {
        Write-Error "$phase が result.json を書きませんでした: $resultPath"
        exit 4
    }
    # トップレベルの success だけを見る。ステップ配列にも同名キーがあるため、
    # 全文正規表現ではどちらか一方が true なだけで合格にしてしまう
    # Read only the top-level success; the steps array carries the same key name,
    # so a whole-text regex would pass when either one alone is true
    $resultJson = Get-Content $resultPath -Raw | ConvertFrom-Json
    if ($resultJson.success -ne $true) {
        Write-Error "$phase の通し検証が失敗しました: $(Get-Content $resultPath -Raw)"
        exit 5
    }
}

Write-Output "smoke completed"
