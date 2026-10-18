#!/usr/bin/env bash
# Steam・削除・検証の境界で本体の有無と証拠の保持を検査する
# Check payload presence and evidence retention at upload, removal, and verification boundaries

assert_payloads_removed() {
    local run="$1" os
    for os in windows mac; do
        [ ! -e "$run/build-$os" ] && [ ! -L "$run/build-$os" ] || fail "$os build survived upload success"
    done
}

assert_payloads_retained() {
    local run="$1" os
    for os in windows mac; do
        [ -d "$run/build-$os" ] || fail "$os build was removed before upload success"
    done
    [ -f "$run/build-windows/build-started" ] || fail "Windows build content was deleted"
}

write_delivery_stubs() {
    local sandbox="$1"
    cat >"$sandbox/bin/steamcmd" <<EOF_STEAM
#!/bin/bash
set -eu
if [ "\$#" -eq 3 ]; then
    echo "steam-auth \$*" >>"$sandbox/calls.log"
    if [ "\${STEAM_AUTH_EXIT:-0}" != 0 ]; then
        echo "Cached credentials not found"
        exit "\$STEAM_AUTH_EXIT"
    fi
    echo "Logged in OK"
    exit 0
fi
echo "steamcmd \$*" >>"$sandbox/calls.log"
while [ "\$1" != +run_app_build ]; do shift; done
run="\$(dirname "\$(dirname "\$2")")"
[ -f "\$run/build-windows/moorestech.exe" ]
[ -f "\$run/build-mac/moorestech.app/Contents/MacOS/moorestech" ]
touch "\$run/steam/output/upload.log"
[ "\${STEAMCMD_EXIT:-0}" = 0 ] || exit "\$STEAMCMD_EXIT"
touch "\$run/steam/upload-complete"
EOF_STEAM
    # 本体2パスへの削除だけを失敗させ、テストの片付けや欠損生成は実rmへ渡す
    # Fail only the two-payload removal, delegating test teardown and missing-artifact setup to real rm
    cat >"$sandbox/bin/rm" <<EOF_RM
#!/bin/bash
if [ "\$#" -eq 4 ] && [ "\$1" = -rf ] && [ "\$2" = -- ] && \\
    [ "\$3" = "\${4%/build-mac}/build-windows" ]; then
    echo "rm \$*" >>"$sandbox/calls.log"
    [ -f "\${3%/build-windows}/steam/upload-complete" ] || exit 92
    case "\${CLEANUP_FAILURE:-none}" in
      full) exit 1 ;;
      partial) /bin/rm -rf -- "\$3"; exit 1 ;;
    esac
fi
exec /bin/rm "\$@"
EOF_RM
    cat >"$sandbox/bin/verify" <<EOF_VERIFY
#!/bin/bash
set -eu
echo "verify \$*" >>"$sandbox/calls.log"
run="\$PLAYTEST_RUN_ROOT/\$1"
for os in windows mac; do
    case "\${CLEANUP_FAILURE:-none}:\$os" in
      full:*|partial:mac) [ -d "\$run/build-\$os" ] ;;
      *) [ ! -e "\$run/build-\$os" ] && [ ! -L "\$run/build-\$os" ] ;;
    esac
done
mkdir -p "\$run/verify"
printf '{"exitCode":%s}\n' "\${VERIFY_EXIT:-0}" >"\$run/verify/result.json"
exit "\${VERIFY_EXIT:-0}"
EOF_VERIFY
}
