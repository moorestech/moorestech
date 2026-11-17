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
    release_require_no_cef_files "$build_dir"
    release_require_build_info "$build_dir/moorestech_Data/StreamingAssets/build-info.json" StandaloneWindows64
}

# CEF ランタイムに固有の名前。撤去後の成果物に 1 つでもあれば古い Library か取り違えの混入
# Names unique to the CEF runtime; any hit in a post-removal artifact means a stale Library or a mixed-up build
CEF_RUNTIME_NAME_PATTERNS=(
    "cef-unity-*"
    "cef_unity_*"
    "libcef*"
    "CefUnity.*"
    "chrome_elf.dll"
    "icudtl.dat"
    "v8_context_snapshot.bin"
    "chrome_*_percent.pak"
)

# 成果物の下に CEF のファイルと撤去前の Web UI が 1 つも無いことを確かめる（統合計画 §5.1-1）
# Check that no CEF file nor pre-removal Web UI remains anywhere under the artifact (integration plan §5.1-1)
release_require_no_cef_files() {
    local root="$1" pattern found find_failed
    for pattern in "${CEF_RUNTIME_NAME_PATTERNS[@]}"; do
        # find の失敗（読めないディレクトリ等）は見落としなので、空の結果を「CEF 0」と読まない
        # A find failure (e.g. an unreadable directory) hides files, so an empty result must not read as "no CEF"
        if found="$(find "$root" -iname "$pattern" -print -quit)"; then find_failed=0; else find_failed=1; fi
        if [ -n "$found" ]; then
            echo "ERROR: 成果物に CEF のファイルが残っています（${pattern}）: ${found}" >&2
            exit 4
        fi
        if [ "$find_failed" -ne 0 ]; then
            echo "ERROR: 成果物の CEF の検査で find が失敗しました（${pattern}）。見落としを避けて止めます: ${root}" >&2
            exit 4
        fi
    done
    # 撤去前のビルドが残した Web の成果物も同じく混入。find の失敗は見落としとして止める
    # Web output left by a pre-removal build is a leak too; a find failure stops as a possible miss
    if found="$(find "$root" -path "*/StreamingAssets/WebUi" -print -quit)"; then find_failed=0; else find_failed=1; fi
    if [ -n "$found" ]; then
        echo "ERROR: 成果物に撤去前の Web UI（StreamingAssets/WebUi）が残っています。Assets/StreamingAssets/WebUi を消して焼き直してください: ${found}" >&2
        exit 4
    fi
    if [ "$find_failed" -ne 0 ]; then
        echo "ERROR: 成果物の StreamingAssets/WebUi の検査で find が失敗しました。見落としを避けて止めます: ${root}" >&2
        exit 4
    fi
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
    local build_dir="$1" app="$1/moorestech.app" archs executable relative_path
    for relative_path in "${MAC_REQUIRED_RELATIVE_PATHS[@]}"; do
        release_require_paths "$build_dir/$relative_path"
    done

    release_require_no_cef_files "$build_dir"
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
    # 本体と録画用ffmpegをApple Silicon専用に揃える
    # Match the player and the recording ffmpeg to Apple Silicon only
    for executable in "$app/Contents/MacOS/moorestech" "$app/Contents/MacOS/ffmpeg"; do
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
