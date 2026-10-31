#!/usr/bin/env bash
# Hermes cron から生データ置き場の期限削除を呼ぶ shim。HERMES_HOME/scripts へ「コピー」して使う（理由は moorestech-playtest-digest.sh 冒頭と同じ）
# stdout は --no-agent でそのまま Discord へ流れる。何も消さなかった回は無言、消した物・消せなかった物だけが出る
# Shim that lets Hermes cron prune the playtest raw store; COPY it into HERMES_HOME/scripts (same reasons as moorestech-playtest-digest.sh)
# With --no-agent stdout goes to Discord verbatim: idle runs print nothing, only deletions and failures appear
set -euo pipefail
REPO="${MOORESTECH_REPO:-/Users/sakastudio/hermes-agent/data/repos/moorestech}"
export MOORESTECH_PLAYTEST_RAW="${MOORESTECH_PLAYTEST_RAW:-/Users/sakastudio/hermes-agent/data/repos/moorestech_playtest_raw}"
exec /usr/bin/python3 "$REPO/scripts/playtest/raw_store/prune.py" "$@"
