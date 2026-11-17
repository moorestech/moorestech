#!/usr/bin/env bash
# release-playtest-windows.sh の契約テスト用に、配布ビルド機への ssh / scp を手元の偽ジョブ置き場へ向けるスタブを作る
# 起動の ssh は引数の base64 JSON を解いて偽 unity を 2 ターゲット分走らせ、result.json を書く。WINBAKE_FAIL=1 で焼きの失敗を返す
# Creates ssh / scp stubs for the release-playtest-windows.sh contract tests that point the build machine at a local fake job area
# The launching ssh decodes the base64 JSON args, runs the fake unity for both targets and writes result.json; WINBAKE_FAIL=1 reports a failed bake

write_winbake_stubs() {
    local sandbox="$1"
    mkdir -p "$sandbox/winhost"
    cat >"$sandbox/bin/ssh" <<EOF
#!/bin/bash
# 宛先より前のオプションを読み飛ばし、残りを Windows 側のコマンドとして扱う
# Skip options before the host, then treat the rest as the Windows-side command
while [ "\$1" = -o ]; do shift 2; done
host="\$1"; shift
cmd="\$*"
local_path() { printf '%s' "\$1" | sed "s#C:/dev/worktrees/moorestech/_release#$sandbox/winhost#"; }
case "\$cmd" in
  *Invoke-CimMethod*)
    echo "ssh \$host launch" >>"$sandbox/calls.log"
    b64="\$(printf '%s' "\$cmd" | sed -n 's/.*-ArgsB64 \([A-Za-z0-9+\/=]*\).*/\1/p')"
    eval "\$(printf '%s' "\$b64" | base64 -d | python3 -c 'import json, shlex, sys; d = json.load(sys.stdin); print("label=%s branch=%s job=%s" % tuple(shlex.quote(d[k]) for k in ("label", "branch", "jobDir")))')"
    job="\$(local_path "\$job")"; mkdir -p "\$job/build-windows" "\$job/build-mac"
    touch "\$job/progress.txt" "\$job/unity-build-windows.log" "\$job/unity-build-mac.log"
    MOORESTECH_BUILD_OUTPUT="\$job/build-windows" MOORESTECH_STEAM_BUILD_LABEL="\$label" MOORESTECH_BUILD_BRANCH="\$branch" "$sandbox/bin/unity" -executeMethod WindowsSteamPlaytestBuild
    # Windows の焼きは実行権を持たないので、偽の Mac 成果物も実行権なしにする
    # A Windows bake carries no executable bits, so the fake Mac artifact has none either
    MAC_NOT_EXECUTABLE=1 MOORESTECH_BUILD_OUTPUT="\$job/build-mac" MOORESTECH_STEAM_BUILD_LABEL="\$label" MOORESTECH_BUILD_BRANCH="\$branch" "$sandbox/bin/unity" -executeMethod MacOsSteamPlaytestBuild
    if [ "\${WINBAKE_FAIL:-0}" = 0 ]; then ok=true; err=null; else ok=false; err='"Unity failed"'; fi
    [ "\${WINBAKE_VANISH:-0}" = 0 ] || exit 0
    printf '{"ok":%s,"error":%s}' "\$ok" "\$err" >"\$job/result.json"
    ;;
  *result.json*)
    # 焼きの台本が result.json を書かずに消えた場合は gone を返し、確かめ直しも失敗させる
    # When the bake script vanished without result.json, answer gone and fail the recheck too
    echo "ssh \$host poll" >>"$sandbox/calls.log"
    result="\$(ls "$sandbox"/winhost/jobs/*/result.json 2>/dev/null | head -n 1)"
    case "\$cmd" in
      *"exit 0 } else"*) [ -n "\$result" ] ;;
      *) if [ -n "\$result" ]; then echo done; else echo gone; fi ;;
    esac
    ;;
  "tar -cf - -C "*)
    remote="\$(printf '%s' "\$cmd" | sed -n 's/^tar -cf - -C "\(.*\)" \.\$/\1/p')"
    echo "ssh \$host tar \$(basename "\$remote")" >>"$sandbox/calls.log"
    tar -cf - -C "\$(local_path "\$remote")" .
    ;;
  *New-Item*) echo "ssh \$host mkdir" >>"$sandbox/calls.log" ;;
  *) echo "unexpected ssh command: \$cmd" >&2; exit 99 ;;
esac
EOF
    cat >"$sandbox/bin/scp" <<EOF
#!/bin/bash
while [ "\$1" = -q ] || [ "\$1" = -o ]; do [ "\$1" = -o ] && shift; shift; done
src="\$1"; dst="\$2"
echo "scp \$(basename "\$src")" >>"$sandbox/calls.log"
case "\$src" in
  *:*) cp "\$(printf '%s' "\${src#*:}" | sed "s#C:/dev/worktrees/moorestech/_release#$sandbox/winhost#")" "\$dst" ;;
  *) mkdir -p "$sandbox/winhost/tools"; cp "\$src" "$sandbox/winhost/tools/" ;;
esac
EOF
    chmod +x "$sandbox/bin/ssh" "$sandbox/bin/scp"
}
