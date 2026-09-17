#!/usr/bin/env bash
# Hermes cron から moorestech の日次ダイジェストを呼ぶ shim。HERMES_HOME/scripts へ「コピー」して使う
# Shim that lets Hermes cron run the moorestech daily digest; it must be COPIED into HERMES_HOME/scripts
#
# Hermes は --script のパスを HERMES_HOME/scripts/ 配下へ解決してから realpath で検査するため、
# repo へのシンボリックリンクは「scripts ディレクトリ外への traversal」として拒否される。
# Hermes resolves --script under HERMES_HOME/scripts/ and then realpath-checks it, so a symlink
# into the repo is rejected as traversal; a real wrapper file is the supported shape.
# 前例 / precedent: ~/.hermes/scripts/monitors/tiktok-collector-monitor/check.sh
#
# ingest-dispatch.sh 等と違い「コピーされた実体」として動くため、スクリプト自身の位置から
# repo を逆算できない（コピー先は HERMES_HOME/scripts で repo の外）。既定値は $HOME にも
# 依存させない絶対パスにする。Hermes ゲートウェイ本体は HOME を封じ込め用ディレクトリへ
# 差し替えて動くため、`~` 展開に頼ると cron 実行時にどちらの HOME で解決されるか不定になる。
# Unlike ingest-dispatch.sh, this copied file cannot derive the repo from its own location (the
# copy lives under HERMES_HOME/scripts, outside the repo). The default must not depend on $HOME
# either: the Hermes gateway process swaps HOME to a containment dir, so a `~`-based default
# would resolve inconsistently depending on which HOME the cron job actually runs under.
set -euo pipefail
REPO="${MOORESTECH_REPO:-/Users/sakastudio/hermes-agent/data/repos/moorestech}"
export MOORESTECH_LOGS="${MOORESTECH_LOGS:-/Users/sakastudio/hermes-agent/data/repos/moorestech_logs}"
exec /usr/bin/python3 "$REPO/scripts/playtest/digest.py" --date yesterday
