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
    mkdir -p "\$app/Contents/MacOS" "\$app/Contents/Resources/Data/StreamingAssets" "\$out/game/mods"
    touch "\$app/Contents/MacOS/moorestech" "\$app/Contents/MacOS/ffmpeg" "\$app/Contents/Resources/ffmpeg-LICENSE.txt"
    case "\${MAC_MISSING_PATH:-}" in
      main) rm "\$app/Contents/MacOS/moorestech" ;;
      ffmpeg) rm "\$app/Contents/MacOS/ffmpeg" ;;
      license) rm "\$app/Contents/Resources/ffmpeg-LICENSE.txt" ;;
    esac
    [ "\${MAC_LEAKS_EVENT_SCRIPT:-0}" = "0" ] || touch "\$out/start-gamescom-loop.command"
    info="\$app/Contents/Resources/Data/StreamingAssets/build-info.json"
    target="\${BUILD_INFO_TARGET_MAC:-StandaloneOSX}"
    ;;
  *)
    [ "\${UNITY_EXIT:-0}" = "0" ] || exit "\${UNITY_EXIT}"
    mkdir -p "\$out/moorestech_Data/StreamingAssets" "\$out/game/mods"
    touch "\$out/moorestech.exe"
    info="\$out/moorestech_Data/StreamingAssets/build-info.json"
    target="\${BUILD_INFO_TARGET_WINDOWS:-StandaloneWindows64}"
    ;;
esac
printf '{"commit":"%s","branch":"%s","steamBuildLabel":"%s","target":"%s"}' \
  "\${BUILD_INFO_COMMIT:-$commit}" "\${BUILD_INFO_BRANCH:-\$MOORESTECH_BUILD_BRANCH}" "\$MOORESTECH_STEAM_BUILD_LABEL" "\$target" >"\$info"
EOF
    cat >"$sandbox/bin/codesign" <<EOF
#!/bin/bash
echo "codesign \$*" >>"$sandbox/calls.log"
exit "\${CODESIGN_EXIT:-0}"
EOF
    cat >"$sandbox/bin/lipo" <<EOF
#!/bin/bash
echo "lipo \$*" >>"$sandbox/calls.log"
case "\$*" in *'/Contents/MacOS/ffmpeg'*) echo "\${LIPO_FFMPEG_ARCHS:-\${LIPO_ARCHS:-arm64}}"; exit 0;; esac
echo "\${LIPO_ARCHS:-arm64}"
EOF
    chmod +x "$sandbox/bin/unity" "$sandbox/bin/codesign" "$sandbox/bin/lipo"
}
