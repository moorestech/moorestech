#!/usr/bin/env bash
# verify-on-windows.sh の契約テストが使うスタブ群と実行関数。test-verify-on-windows.sh から source する
# Stubs and runner for the verify-on-windows.sh contract tests, sourced by test-verify-on-windows.sh

TARGET="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)/verify-on-windows.sh"
FAILURES=0
LABEL="playtest-20260913-1730"

fail() { echo "FAIL: $1"; FAILURES=$((FAILURES + 1)); }

# 各make_sandboxが作った一時ディレクトリを配列に積み、終了時にまとめて削除する
# Every temp dir created by make_sandbox is tracked here and removed together on exit
SANDBOXES=()
cleanup() { for dir in "${SANDBOXES[@]}"; do rm -rf "$dir"; done; }
trap cleanup EXIT

make_sandbox() {
    SANDBOX="$(mktemp -d)"
    SANDBOXES+=("$SANDBOX")
    mkdir -p "$SANDBOX/bin" "$SANDBOX/artifacts"
    cat >"$SANDBOX/bin/wakeonlan" <<EOF
#!/bin/bash
echo "wakeonlan \$*" >>"$SANDBOX/calls.log"
exit \${WOL_EXIT:-0}
EOF
    # sshは SSH_READY_AT 回目の呼び出しから成功する（到達待ちのループを再現する）
    # ssh starts succeeding at call number SSH_READY_AT, reproducing the reachability loop
    cat >"$SANDBOX/bin/ssh" <<EOF
#!/bin/bash
echo "ssh \$*" >>"$SANDBOX/calls.log"
count=\$(grep -c '^ssh ' "$SANDBOX/calls.log")
# 未到達の回は SSH_UNREACHABLE_DELAY 秒かけて失敗する（ConnectTimeout で実時間が進むことを再現する）
# An unreachable call takes SSH_UNREACHABLE_DELAY seconds to fail, reproducing wall time spent in ConnectTimeout
if [ "\$count" -lt "\${SSH_READY_AT:-1}" ]; then sleep "\${SSH_UNREACHABLE_DELAY:-0}"; exit 255; fi
# 検証機側 run-smoke.ps1 の実行だけ SMOKE_RUN_EXIT で終了コードを差し替える（ラベル不一致=2 等の伝搬を固定する）
# Only the run-smoke.ps1 invocation takes SMOKE_RUN_EXIT, pinning propagation of e.g. the label-mismatch exit 2
case "\$*" in *"-File"*"run-smoke.ps1"*) exit \${SMOKE_RUN_EXIT:-0} ;; esac
exit \${SSH_RUN_EXIT:-0}
EOF
    # scpはディレクトリごとコピーする実装（-r remote:.../results dest）に合わせ、
    # destの直下ではなく dest/results/<phase> へ result.json を置く
    # scp copies the directory itself (-r remote:.../results dest), so results land under
    # dest/results/<phase>, mirroring scp's real nesting behavior when the dest already exists
    cat >"$SANDBOX/bin/scp" <<EOF
#!/bin/bash
echo "scp \$*" >>"$SANDBOX/calls.log"
# 最後の引数がローカルの回収先なら、検証機が残したはずの result.json を再現する
# When the last argument is the local collection directory, reproduce the result.json the machine would leave
for last; do :; done
case "\$last" in
  "$SANDBOX"/artifacts*)
    dest="\$last/results"
    mkdir -p "\$dest/phase1"
    # stepsにトップレベルとは独立した"success"キーを持たせ、全文一致(旧grep実装)が
    # トップレベルfalseでも合格にしてしまう退行を検知できるようにする
    # steps carries its own independent "success" key so a whole-text match (the old grep
    # implementation) regressing into a pass despite a false top-level value can be caught
    printf '{"phase": "phase1", "success": %s, "steps": [{"name": "tutorial", "success": true}]}' \
      "\${PHASE1_SUCCESS:-true}" >"\$dest/phase1/result.json"
    # reportBundleDirectoryは検証機(Windows)のパス。バックスラッシュ区切りでIDを末尾に持たせ、
    # basenameがそのままでは割れないことを再現する
    # reportBundleDirectory is the check machine's (Windows) path; backslash-separated with the id
    # as the leaf, reproducing that a plain basename cannot split it
    # JSON中のバックスラッシュは \\ とエスケープしないと不正なJSONになる(実物はJsonUtilityがエスケープ済みで書く)。
    # 外側がクォート無しheredocのため \\\\ の4つで初めてスタブ内に \\ の2文字が残る
    # A backslash inside JSON must be escaped as \\, or the text is invalid JSON (the real writer, JsonUtility,
    # already escapes it); since the outer heredoc is unquoted, 4 backslashes are needed to leave 2 in the stub
    BS='\\\\'
    # SCP_OMIT_PHASE2=1 は検証機が phase2 の result.json を残さなかった回を再現する
    # SCP_OMIT_PHASE2=1 reproduces a run where the machine left no phase2 result.json
    [ "\${SCP_OMIT_PHASE2:-0}" = "1" ] && exit 0
    mkdir -p "\$dest/phase2"
    printf '{"phase": "phase2", "success": %s, "steps": [{"name": "save", "success": true}], "reportBundleDirectory": "C:%smoorestech-smoke%soutbox%sreport%s20260913_180000_%s"%s}' \
      "\${PHASE2_SUCCESS:-true}" "\$BS" "\$BS" "\$BS" "\$BS" "\${SMOKE_REPORT_ID:-aaaa1111}" "\${REPORT_STEAM_ID_FIELD-, \"reportSteamId\": \"7656\"}" >"\$dest/phase2/result.json"
    ;;
esac
exit 0
EOF
    # 受け口 lib の curl 呼び出し（-w '%{http_code}' -o <out> ... <url>）を模す。READY は RECEIVER_HAS_REPORT=1 のとき
    # report/7656/20260913_180000_<SMOKE_REPORT_ID> にだけ在り、それ以外の steamId/id は 404 を返す。ACK は ACK_STATUS を返す
    # Mimics the receiver lib's curl call (-w '%{http_code}' -o <out> ... <url>). READY exists only at
    # report/7656/20260913_180000_<SMOKE_REPORT_ID> when RECEIVER_HAS_REPORT=1, any other steamId/id is 404; ACK returns ACK_STATUS
    cat >"$SANDBOX/bin/curl" <<EOF
#!/bin/bash
echo "curl \$*" >>"$SANDBOX/calls.log"
out=/dev/null
prev=""
for arg; do [ "\$prev" = "-o" ] && out="\$arg"; prev="\$arg"; url="\$arg"; done
expected="https://playtest.moores.tech/v1/inbox/report/7656/20260913_180000_\${SMOKE_REPORT_ID:-aaaa1111}"
case "\$url" in
  "\$expected/READY")
    if [ "\${RECEIVER_HAS_REPORT:-1}" = "1" ]; then echo '{"files":[]}' >"\$out"; printf 200; else echo nf >"\$out"; printf 404; fi ;;
  "\$expected/ack") printf "\${ACK_STATUS:-200}" ;;
  *) echo nf >"\$out"; printf 404 ;;
esac
EOF
    chmod +x "$SANDBOX/bin/"*
}

run_target() {
    ( MOORESTECH_VERIFY_HOST=verify-pc MOORESTECH_VERIFY_USER=moores \
      MOORESTECH_VERIFY_MAC=00:11:22:33:44:55 \
      PLAYTEST_RECEIVER_BASE=https://playtest.moores.tech \
      PLAYTEST_ADMIN_KEY=dummy \
      WAKEONLAN_BIN="$SANDBOX/bin/wakeonlan" SSH_BIN="$SANDBOX/bin/ssh" \
      SCP_BIN="$SANDBOX/bin/scp" CURL_CMD="$SANDBOX/bin/curl" \
      VERIFY_ARTIFACT_ROOT="$SANDBOX/artifacts" \
      SSH_WAIT_TIMEOUT_SECONDS="${SSH_WAIT_TIMEOUT_SECONDS-60}" SSH_POLL_SECONDS="${SSH_POLL_SECONDS-0}" \
      SSH_UNREACHABLE_DELAY="${SSH_UNREACHABLE_DELAY-0}" REPORT_STEAM_ID_FIELD="${REPORT_STEAM_ID_FIELD-, \"reportSteamId\": \"7656\"}" \
      WOL_EXIT="${WOL_EXIT-0}" SSH_READY_AT="${SSH_READY_AT-1}" SSH_RUN_EXIT="${SSH_RUN_EXIT-0}" \
      PHASE1_SUCCESS="${PHASE1_SUCCESS-true}" PHASE2_SUCCESS="${PHASE2_SUCCESS-true}" \
      RECEIVER_HAS_REPORT="${RECEIVER_HAS_REPORT-1}" SMOKE_REPORT_ID="${SMOKE_REPORT_ID-aaaa1111}" \
      ACK_STATUS="${ACK_STATUS-200}" \
      SMOKE_RUN_EXIT="${SMOKE_RUN_EXIT-0}" SCP_OMIT_PHASE2="${SCP_OMIT_PHASE2-0}" \
      bash "$TARGET" "${VERIFY_LABEL-$LABEL}" 2>&1 )
}

