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

release_require_mac_artifact() {
    local build_dir="$1" app="$1/moorestech.app" archs
    release_require_paths "$app/Contents/MacOS/moorestech" "$app/Contents/MacOS/ffmpeg" "$app/Contents/Resources/ffmpeg-LICENSE.txt" \
        "$build_dir/game/mods" "$app/Contents/Resources/Data/StreamingAssets/build-info.json"
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
    # CEF の Mac ランタイムに合わせ主実行ファイルを arm64 のみにする
    # Match the main executable to the arm64-only Mac CEF runtime
    if ! archs="$("$LIPO_BIN" -archs "$app/Contents/MacOS/moorestech")"; then
        echo "ERROR: Mac成果物のアーキテクチャを取得できません: ${app}" >&2
        exit 4
    fi
    if [ "$archs" != "arm64" ]; then
        echo "ERROR: Mac成果物のアーキテクチャが arm64 のみではありません（${archs}）: ${app}" >&2
        exit 4
    fi
    release_require_build_info "$app/Contents/Resources/Data/StreamingAssets/build-info.json" StandaloneOSX
}
