#!/usr/bin/env bash
# 配布ビルド機（Windows・ssh の宛先 MOORESTECH_WINBUILD_HOST）で Windows 版と Mac 版を焼き、RUN_DIR の build-windows / build-mac へ持ち帰る
# 呼び出し側が RUN_DIR・COMMIT_FULL・BUILD_LABEL・BUILD_BRANCH・SCRIPT_DIR を用意する。ssh は BatchMode で、焼きは Windows 側で切り離して走らせる
# Bakes the Windows and Mac players on the Windows build machine (ssh target MOORESTECH_WINBUILD_HOST) and brings them back into RUN_DIR's build-windows / build-mac
# Callers provide RUN_DIR, COMMIT_FULL, BUILD_LABEL, BUILD_BRANCH and SCRIPT_DIR; ssh runs in BatchMode and the bake runs detached on Windows
WINBAKE_REMOTE_ROOT='C:/dev/worktrees/moorestech/_release'
# 1 回の焼きの上限（Library の複製・2 ターゲット分の import と焼きを含む）
# Upper bound for one bake (Library copy plus import and bake for two targets)
WINBAKE_TIMEOUT_SECONDS="${WINBAKE_TIMEOUT_SECONDS:-14400}"
WINBAKE_POLL_SECONDS="${WINBAKE_POLL_SECONDS:-30}"

winbake_ssh() { "${SSH_BIN:-ssh}" -o BatchMode=yes -o ConnectTimeout=20 -o ServerAliveInterval=15 -o ServerAliveCountMax=4 "$MOORESTECH_WINBUILD_HOST" "$@"; }

# コミット済みピンから同梱元 master と非公開アセットのコミットを読む（Mac で焼くときと同じ正本）
# Read the master and private-asset commits from the committed pins, the same source of truth as a Mac bake
winbake_read_pin() {
    "$GIT_BIN" -C "$SCRIPT_DIR" show "$COMMIT_FULL:.moorestech-external-revisions.json" | python3 -c '
import json, sys
pins = {r["key"]: r["commitHash"] for r in json.load(sys.stdin)["repositories"]}
print(pins[sys.argv[1]])' "$1"
}

release_bake_on_windows() {
    local master_pin private_pin job_dir args_b64 state deadline
    if [ -z "${MOORESTECH_WINBUILD_HOST:-}" ]; then
        echo "ERROR: MOORESTECH_WINBUILD_HOST（配布ビルド機の ssh 宛先）が必要です" >&2
        exit 2
    fi
    master_pin="$(winbake_read_pin moorestech_master)"
    private_pin="$(winbake_read_pin moorestech_client_private)"
    job_dir="$WINBAKE_REMOTE_ROOT/jobs/$BUILD_LABEL"
    echo "[release-playtest] windows bake host=$MOORESTECH_WINBUILD_HOST job=$job_dir master=$master_pin private=$private_pin"

    # 焼く台本は毎回送る（手置きコピーとの版ずれを防ぐ）
    # Ship the bake script every run to avoid drift from a hand-placed copy
    winbake_ssh "powershell -NoProfile -Command \"New-Item -ItemType Directory -Force -Path '$WINBAKE_REMOTE_ROOT/tools','$job_dir' | Out-Null\""
    "${SCP_BIN:-scp}" -q -o BatchMode=yes "$SCRIPT_DIR/windows/bake-players.ps1" "$MOORESTECH_WINBUILD_HOST:$WINBAKE_REMOTE_ROOT/tools/bake-players.ps1"
    args_b64="$(python3 -c 'import json, sys, base64; k = ["commit", "masterPin", "privatePin", "label", "branch", "jobDir"]; d = dict(zip(k, sys.argv[1:])); d["targets"] = ["StandaloneWindows64", "StandaloneOSX"]; print(base64.b64encode(json.dumps(d).encode()).decode())' \
        "$COMMIT_FULL" "$master_pin" "$private_pin" "$BUILD_LABEL" "$BUILD_BRANCH" "$job_dir")"

    # ssh が切れても焼きが続くよう、Win32_Process.Create で ssh のセッションの外に起こす
    # Start it outside the ssh session via Win32_Process.Create, so a dropped ssh never kills the bake
    winbake_ssh "powershell -NoProfile -Command \"\$r = Invoke-CimMethod Win32_Process -MethodName Create -Arguments @{CommandLine='powershell -NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File $WINBAKE_REMOTE_ROOT/tools/bake-players.ps1 -ArgsB64 $args_b64'}; if (\$r.ReturnValue -ne 0) { exit 1 }\""

    # result.json ができるまで待つ。途中経過は progress.txt の末尾を出す
    # Wait for result.json, echoing the tail of progress.txt along the way
    deadline=$((SECONDS + WINBAKE_TIMEOUT_SECONDS))
    while :; do
        # 焼きの PowerShell が result.json を書かずに消えていたら（構文エラー・強制終了）待ち続けずに止める。自分の PID は除く
        # Stop instead of waiting on when the bake PowerShell vanished without result.json (parse error, killed); exclude our own PID
        state="$(winbake_ssh "powershell -NoProfile -Command \"if (Test-Path '$job_dir/result.json') { 'done' } elseif (-not (Get-CimInstance Win32_Process -Filter \\\"Name='powershell.exe'\\\" | Where-Object { \$_.ProcessId -ne \$PID -and \$_.CommandLine -like '*$args_b64*' })) { 'gone' } else { 'running: ' + (Get-Content '$job_dir/progress.txt' -Tail 1 -ErrorAction SilentlyContinue) }; exit 0\"" | tr -d '\r')" || state="(ssh failed; retrying)"
        [ "$state" = done ] && break
        if [ "$state" = gone ]; then
            sleep 5
            winbake_ssh "powershell -NoProfile -Command \"if (Test-Path '$job_dir/result.json') { exit 0 } else { exit 1 }\"" && break
            echo "ERROR: Windows の焼きが result.json を書かずに終わりました（台本の構文エラーか強制終了）: $MOORESTECH_WINBUILD_HOST:$job_dir" >&2
            exit 3
        fi
        echo "[release-playtest] windows bake: $state"
        if [ "$SECONDS" -gt "$deadline" ]; then
            echo "ERROR: Windows の焼きが ${WINBAKE_TIMEOUT_SECONDS} 秒で終わりませんでした: $MOORESTECH_WINBUILD_HOST:$job_dir" >&2
            exit 3
        fi
        sleep "$WINBAKE_POLL_SECONDS"
    done
    winbake_fetch_file "$job_dir/result.json" "$RUN_DIR/windows-bake-result.json"
    if ! python3 -c 'import json, sys; sys.exit(0 if json.load(open(sys.argv[1], encoding="utf-8-sig"))["ok"] else 1)' "$RUN_DIR/windows-bake-result.json"; then
        echo "ERROR: Windows の焼きが失敗しました: $(python3 -c 'import json, sys; print(json.load(open(sys.argv[1], encoding="utf-8-sig"))["error"])' "$RUN_DIR/windows-bake-result.json")" >&2
        winbake_fetch_logs "$job_dir"
        exit 3
    fi
    winbake_fetch_logs "$job_dir"
    winbake_fetch_dir "$job_dir/build-windows" "$RUN_DIR/build-windows"
    winbake_fetch_dir "$job_dir/build-mac" "$RUN_DIR/build-mac"
    winbake_restore_mac_executable_bits "$RUN_DIR/build-mac/moorestech.app"
}

winbake_fetch_file() { "${SCP_BIN:-scp}" -q -o BatchMode=yes "$MOORESTECH_WINBUILD_HOST:$1" "$2"; }

# Unity のログは失敗の調査に要るので、取れなかったら警告して続ける（本体の判定は result.json で済んでいる）
# Unity logs matter for diagnosis, so a failed fetch warns and continues (the verdict already came from result.json)
winbake_fetch_logs() {
    local name
    for name in unity-build-windows.log unity-build-mac.log progress.txt; do
        winbake_fetch_file "$1/$name" "$RUN_DIR/$name" || echo "WARNING: Windows から $name を取れませんでした: $1/$name" >&2
    done
}

# Windows の tar で固めて流し、構成を変えずに展開する
# Stream it through Windows tar and extract without changing the layout
winbake_fetch_dir() {
    local remote="$1" local_dir="$2"
    mkdir -p "$local_dir"
    if ! winbake_ssh "tar -cf - -C \"$remote\" ." | tar -xf - -C "$local_dir"; then
        echo "ERROR: Windows の成果物を持ち帰れませんでした: $remote" >&2
        exit 3
    fi
}

# Windows のファイルシステムは実行権を持たないので、.app の Contents/MacOS にある実行ファイルへ付け直す（検査は release_require_mac_artifact）
# Windows file systems drop the executable bit, so restore it on the .app's Contents/MacOS files (checked by release_require_mac_artifact)
winbake_restore_mac_executable_bits() {
    local macos_dir="$1/Contents/MacOS"
    if [ ! -d "$macos_dir" ]; then
        echo "ERROR: Mac 成果物に Contents/MacOS がありません: $macos_dir" >&2
        exit 4
    fi
    find "$macos_dir" -type f -exec chmod +x {} +
}
