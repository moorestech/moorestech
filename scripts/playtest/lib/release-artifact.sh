#!/usr/bin/env bash
# 欠損・取り違え・署名や CPU の合わない成果物を Steam へ上げない
# Reject incomplete, mixed-up, badly signed or wrong-CPU artifacts before Steam upload

release_require_paths() {
    local path
    for path in "$@"; do
        if [ ! -e "$path" ]; then
            echo "ERROR: 成果物に $path がありません" >&2
            exit 4
        fi
    done
}

# build-info の値を照合し、stale worktree や取り違えを検出する
# Check build-info values to detect a stale worktree or mixed-up artifact
release_require_build_info() {
    local build_info="$1" expected_target="$2"
    if ! BUILD_LABEL="$BUILD_LABEL" COMMIT="$COMMIT_FULL" BRANCH="$BUILD_BRANCH" TARGET="$expected_target" python3 -c '
import json, os, sys
info = json.load(open(sys.argv[1]))
for key, env in (("steamBuildLabel", "BUILD_LABEL"), ("commit", "COMMIT"), ("branch", "BRANCH"), ("target", "TARGET")):
    if info.get(key) != os.environ[env]:
        print(f"{key} mismatch: got {info.get(key)!r} want {os.environ[env]!r}", file=sys.stderr)
        sys.exit(1)
' "$build_info"; then
        echo "ERROR: build-info.json の内容が指定コミット/ラベル/ターゲットと一致しません: ${build_info}" >&2
        exit 4
    fi
}

release_require_windows_artifact() {
    local build_dir="$1"
    release_require_paths "$build_dir/moorestech.exe" "$build_dir/game/mods" "$build_dir/moorestech_Data/StreamingAssets/build-info.json"
    release_require_build_info "$build_dir/moorestech_Data/StreamingAssets/build-info.json" StandaloneWindows64
}

# Mac 成果物に必ず入っている相対パス。検査項目の追加はここ 1 箇所で済ませる
# The relative paths a Mac artifact must always contain; adding a check happens here alone
MAC_REQUIRED_RELATIVE_PATHS=(
    "moorestech.app/Contents/MacOS/moorestech"
    "moorestech.app/Contents/MacOS/ffmpeg"
    "moorestech.app/Contents/Resources/ffmpeg-LICENSE.txt"
    "moorestech.app/Contents/Resources/Data/StreamingAssets/build-info.json"
    "game/mods"
)

release_require_mac_artifact() {
    local build_dir="$1" app="$1/moorestech.app" archs executable relative_path helper_executable
    for relative_path in "${MAC_REQUIRED_RELATIVE_PATHS[@]}"; do
        release_require_paths "$build_dir/$relative_path"
    done

    # CEF helper が無いと Web UI がまるごと起動しないため、実在と実行権まで見る
    # Without the CEF helper the whole Web UI fails to start, so check that it exists and can run
    helper_executable="$(find "$app" -type f -path '*/cef-unity-server.app/Contents/MacOS/cef-unity-server' -print -quit 2>/dev/null)"
    if [ -z "$helper_executable" ]; then
        echo "ERROR: Mac成果物にCEF helper (cef-unity-server.app) がありません: ${app}" >&2
        exit 4
    fi
    if [ ! -x "$helper_executable" ]; then
        echo "ERROR: Mac成果物のCEF helperに実行権がありません: ${helper_executable}" >&2
        exit 4
    fi
    # 展示会用の再起動ループが混ざっていたら用途の取り違え
    # An exhibition restart loop means the build purpose was mixed up
    if [ -e "$build_dir/start-gamescom-loop.command" ]; then
        echo "ERROR: Steam配布のMac成果物に展示会用スクリプトが入っています: ${build_dir}/start-gamescom-loop.command" >&2
        exit 4
    fi
    if ! "$CODESIGN_BIN" --verify --deep --strict "$app"; then
        echo "ERROR: Mac成果物の署名検証に失敗しました: ${app}" >&2
        exit 4
    fi
    # 本体・録画用ffmpeg・CEF helperをApple Silicon専用に揃える
    # Match the player, the recording ffmpeg and the CEF helper to Apple Silicon only
    for executable in "$app/Contents/MacOS/moorestech" "$app/Contents/MacOS/ffmpeg" "$helper_executable"; do
        if ! archs="$("$LIPO_BIN" -archs "$executable")"; then
            echo "ERROR: Mac成果物のアーキテクチャを取得できません: ${executable}" >&2
            exit 4
        fi
        if [ "$archs" != "arm64" ]; then
            echo "ERROR: Mac成果物のアーキテクチャが arm64 のみではありません（${archs}）: ${executable}" >&2
            exit 4
        fi
    done
    release_require_build_info "$app/Contents/Resources/Data/StreamingAssets/build-info.json" StandaloneOSX
}
