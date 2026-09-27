#!/usr/bin/env bash
# 配布契約テスト向けに Unity と Mac 成果物検査のスタブを作る
# Create Unity and Mac artifact-check stubs for the release contract tests

write_build_stubs() {
    local sandbox="$1" commit="$2"
    # 実行メソッドから OS を判別し、対応する成果物と出所情報を作る
    # Select the OS by executeMethod and create its artifact and origin metadata
    cat >"$sandbox/bin/unity" <<EOF
#!/bin/bash
echo "unity \$* branch=\$MOORESTECH_BUILD_BRANCH masterRoot=\$MOORESTECH_MASTER_DATA_ROOT" >>"$sandbox/calls.log"
out="\$MOORESTECH_BUILD_OUTPUT"
case "\$*" in
  *MacOsSteamPlaytestBuild*)
    [ "\${UNITY_MAC_EXIT:-0}" = "0" ] || exit "\${UNITY_MAC_EXIT}"
    app="\$out/moorestech.app"
    helper="\$app/Contents/PlugIns/cef-unity-server.app/Contents/MacOS/cef-unity-server"
    mkdir -p "\$app/Contents/MacOS" "\$app/Contents/Resources/Data/StreamingAssets" "\$out/game/mods" "\$(dirname "\$helper")"
    touch "\$app/Contents/MacOS/moorestech" "\$app/Contents/MacOS/ffmpeg" "\$app/Contents/Resources/ffmpeg-LICENSE.txt" "\$helper"
    chmod +x "\$helper"
    [ "\${MAC_HELPER_NOT_EXECUTABLE:-0}" = "0" ] || chmod -x "\$helper"
    remove_after_write="\${MAC_MISSING_PATH:-}"
    [ "\${MAC_LEAKS_EVENT_SCRIPT:-0}" = "0" ] || touch "\$out/start-gamescom-loop.command"
    info="\$app/Contents/Resources/Data/StreamingAssets/build-info.json"
    target="\${BUILD_INFO_TARGET_MAC:-StandaloneOSX}"
    ;;
  *)
    [ "\${UNITY_EXIT:-0}" = "0" ] || exit "\${UNITY_EXIT}"
    remove_after_write=""
    mkdir -p "\$out/moorestech_Data/StreamingAssets" "\$out/game/mods"
    touch "\$out/moorestech.exe"
    info="\$out/moorestech_Data/StreamingAssets/build-info.json"
    target="\${BUILD_INFO_TARGET_WINDOWS:-StandaloneWindows64}"
    ;;
esac
printf '{"commit":"%s","branch":"%s","steamBuildLabel":"%s","target":"%s"}' \
  "\${BUILD_INFO_COMMIT:-$commit}" "\${BUILD_INFO_BRANCH:-\$MOORESTECH_BUILD_BRANCH}" "\$MOORESTECH_STEAM_BUILD_LABEL" "\$target" >"\$info"
# 欠損ケースは検査対象の相対パスをそのまま消す。build-info も消せるよう書き出しの後に行う
# A missing case removes the checked relative path as-is, after the write so build-info can be removed too
[ -z "\$remove_after_write" ] || rm -rf "\$out/\$remove_after_write"
EOF
    cat >"$sandbox/bin/codesign" <<EOF
#!/bin/bash
echo "codesign \$*" >>"$sandbox/calls.log"
exit "\${CODESIGN_EXIT:-0}"
EOF
    cat >"$sandbox/bin/lipo" <<EOF
#!/bin/bash
echo "lipo \$*" >>"$sandbox/calls.log"
case "\$*" in
  *'/Contents/MacOS/ffmpeg'*) echo "\${LIPO_FFMPEG_ARCHS:-\${LIPO_ARCHS:-arm64}}"; exit 0;;
  *'cef-unity-server'*) echo "\${LIPO_HELPER_ARCHS:-\${LIPO_ARCHS:-arm64}}"; exit 0;;
esac
echo "\${LIPO_ARCHS:-arm64}"
EOF
    chmod +x "$sandbox/bin/unity" "$sandbox/bin/codesign" "$sandbox/bin/lipo"
}
