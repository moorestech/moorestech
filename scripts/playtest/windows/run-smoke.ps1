# 検証機の対話セッションで Steam を再起動し、配布ビルドの通し検証（phase1/phase2）を Steam 経由で実行する（更新は phase1 の起動時に当たる）
# SteamUser 引数は使わない。Steam はテスター用アカウントで事前ログイン済み前提（README 手順6）
# 終了コード: 2=起動したビルドのラベル不一致 3=ゲーム未インストール 4=result.json 無し 5=検証失敗 6=フェーズ期限切れ 7=steam.exe 不在 8=対話セッションで起動できない
# Restarts Steam in the check machine's interactive session and runs the distribution smoke (phase1/phase2) through Steam (phase1's launch applies the update)
# No SteamUser parameter: Steam is expected to be pre-logged-in with the tester account (README step 6)
# Exit codes: 2=launched build label mismatch 3=game not installed 4=no result.json 5=smoke failed 6=phase timed out 7=steam.exe missing 8=cannot launch in the interactive session
param(
    [Parameter(Mandatory = $true)][string]$ResultRoot,
    [Parameter(Mandatory = $true)][string]$ExpectedBuildLabel
)

$ErrorActionPreference = "Stop"
$AppId = "1958160"
$GameProcessName = "moorestech"
# フェーズ期限は、クライアント内部期限の合計が最長になる phase2（起動ゲート180+初期化120+キャプチャ180+アップロード300=780秒。
# phase1 は起動ゲート180+初期化120+セーブ180=480秒）に、Steam 起動・更新確認・プロセス終了待ちの余裕240秒を足した値。
# 内部期限より短いと、クライアントが理由付きで失敗を書く前にここで打ち切ってしまう
# The phase deadline is the longest client-side deadline sum, phase2 (launch gate 180 + init 120 + capture 180 + upload 300 = 780s;
# phase1 is gate 180 + init 120 + save 180 = 480s), plus 240s headroom for Steam launch, update checks and process exit.
# Anything shorter kills the client before it can write its own failure reason
$PhaseTimeoutSeconds = 1020

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
        # 検出と停止の間にゲームが自分で終わると Stop-Process が「プロセスが見つからない」で終了エラーを投げ、
        # Stop 下ではそれが呼び出し元の意図した終了コードを潰して 1 で抜けてしまう。この競合だけは無害化し、
        # 本当の失敗（権限不足等）は Stop-Process が書くエラーで見える
        # If the game quits by itself between detection and stop, Stop-Process throws a terminating
        # "process not found" error; under Stop that would clobber the caller's intended exit code with 1.
        # Only this race is muted here; a real failure (e.g. permission) still surfaces via Stop-Process's error
        $leftover | Stop-Process -Force -ErrorAction SilentlyContinue
    }
}

$SteamExe = Resolve-SteamExe
$GameDirectory = Join-Path (Split-Path $SteamExe -Parent) "steamapps\common\moorestech"
$GameExe = Join-Path $GameDirectory "moorestech.exe"
$BuildInfoPath = Join-Path $GameDirectory "moorestech_Data\StreamingAssets\build-info.json"
Write-Output "steam.exe: $SteamExe"

if (Test-Path $ResultRoot) { Remove-Item $ResultRoot -Recurse -Force }
New-Item -ItemType Directory -Path $ResultRoot | Out-Null

# ssh はセッション0で動き、そこから起動した Steam/ゲームはデスクトップを持たず ssh 終了と共に消える。
# ログイン中の対話セッションで動かすため、使い捨てのスケジュールタスク（cmd /c start で即終了し多重起動拒否に掛からない）を介して起動する
# ssh runs in session 0, where a launched Steam/game has no desktop and dies with the ssh session.
# To run in the logged-in interactive session, launch through a disposable scheduled task (cmd /c start exits at once, so it never trips the multiple-instance refusal)
function Start-SteamInInteractiveSession([string]$Arguments) {
    $taskName = "moorestech-smoke-launch-" + [Guid]::NewGuid().ToString("N").Substring(0, 8)
    $action = New-ScheduledTaskAction -Execute "cmd.exe" -Argument "/c start `"`" `"$SteamExe`" $Arguments"
    $principal = New-ScheduledTaskPrincipal -UserId (whoami) -LogonType Interactive
    Register-ScheduledTask -TaskName $taskName -Action $action -Principal $principal -Force | Out-Null
    Start-ScheduledTask -TaskName $taskName
    Start-Sleep -Seconds 5
    $taskResult = (Get-ScheduledTaskInfo -TaskName $taskName).LastTaskResult
    Unregister-ScheduledTask -TaskName $taskName -Confirm:$false
    if ($taskResult -ne 0) {
        Fail 8 "対話セッションで Steam を起動できませんでした（タスク結果 $taskResult。検証機に誰もログインしていない可能性。自動ログインを確認すること）: $Arguments"
    }
}

# Steam の自動更新は数時間先へ予約されるため待っても来ない。Steam を再起動してアプリ情報を取り直させ、
# 更新そのものは phase1 の -applaunch に任せる（Steam は起動前に保留中の更新を必ず当てる）
# Steam schedules auto-updates hours ahead, so waiting never gets one. Restart Steam to refresh app info and
# leave the update itself to phase1's -applaunch (Steam always applies a pending update before launching)
Stop-LeftoverGame "before restarting Steam"
if (Get-Process -Name steam -ErrorAction SilentlyContinue) {
    Start-SteamInInteractiveSession "-shutdown"
    $shutdownDeadline = (Get-Date).AddSeconds(90)
    while ((Get-Process -Name steam -ErrorAction SilentlyContinue) -and ((Get-Date) -lt $shutdownDeadline)) { Start-Sleep -Seconds 3 }
}
Start-SteamInInteractiveSession "-silent"
Start-Sleep -Seconds 30

# 起動したゲームが期待ラベルのビルドかを build-info.json で確かめる。更新が当たらず旧ビルドが起動したら検証の意味が無い
# Confirm through build-info.json that the launched game is the expected build; an old build launching would void the verification
function Assert-ExpectedBuildLabel {
    $seenLabel = "(build-info.json が無い)"
    if (Test-Path $BuildInfoPath) { $seenLabel = "$((Get-Content $BuildInfoPath -Raw | ConvertFrom-Json).steamBuildLabel)" }
    if ($seenLabel -ne $ExpectedBuildLabel) {
        Stop-LeftoverGame "unexpected build label"
        Fail 2 "起動したビルドのラベルが違います（期待: $ExpectedBuildLabel、実際: $seenLabel。Steam が更新を当てずに起動した。Steam のダウンロード状況を確認すること）: $BuildInfoPath"
    }
}

if (-not (Test-Path $GameExe)) {
    Fail 3 "moorestech.exe が見つかりません（Steam で playtest ブランチをインストールしておくこと）: $GameExe"
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
    Start-SteamInInteractiveSession "-applaunch $AppId --playtestSmoke --smokePhase $phase --smokeResultDirectory $phaseDirectory"

    # result.json が出て、かつゲームが自分で終了するまで待つ。書きかけの読み取りと、次フェーズ起動時の二重起動を避けるため
    # Wait until result.json exists AND the game has quit by itself, avoiding a half-written read and a double launch in the next phase
    # phase1 だけは Steam が起動前に当てる更新のダウンロード時間（最長20分）を期限へ足す
    # Only phase1 adds the download time of the update Steam applies before launching (up to 20 minutes)
    $updateAllowanceSeconds = if ($phase -eq "phase1") { 1200 } else { 0 }
    $phaseDeadline = (Get-Date).AddSeconds($PhaseTimeoutSeconds + $updateAllowanceSeconds)
    $finished = $false
    $labelChecked = $false
    while ((Get-Date) -lt $phaseDeadline) {
        $running = Get-Process -Name $GameProcessName -ErrorAction SilentlyContinue
        if (-not $labelChecked -and ($running -or (Test-Path $resultPath))) { Assert-ExpectedBuildLabel; $labelChecked = $true }
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
