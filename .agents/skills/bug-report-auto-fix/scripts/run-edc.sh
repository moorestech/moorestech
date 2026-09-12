#!/usr/bin/env bash
# バンドルとサーバーディレクトリをスニペットへ埋め込んで uloop execute-dynamic-code を実行する
# Substitutes the bundle and server directory into a snippet and runs it via uloop execute-dynamic-code
set -euo pipefail

if [ "$#" -lt 3 ]; then
  echo "usage: run-edc.sh <unity-project-path> <snippet.cs> <bundle-dir> [<server-data-dir>]" >&2
  exit 2
fi

PROJECT="$1"; SNIPPET="$2"; BUNDLE="$3"; SERVER_DIR="${4:-}"

# 置換元が無い・箱が無いまま実行すると Unity 側で意味不明な失敗になるので、ここで理由を出して落とす
# Running without the snippet or the bundle would fail unintelligibly inside Unity, so bail out here with the reason
[ -f "$SNIPPET" ] || { echo "ERROR: スニペットがありません: $SNIPPET" >&2; exit 2; }
[ -d "$BUNDLE" ] || { echo "ERROR: バンドルディレクトリがありません: $BUNDLE" >&2; exit 2; }
if grep -q '__SERVER_DIR__' "$SNIPPET" && [ -z "$SERVER_DIR" ]; then
  echo "ERROR: このスニペットは __SERVER_DIR__ を使いますが、サーバーデータディレクトリが渡されていません: $SNIPPET" >&2
  exit 2
fi

WORK="$(mktemp -d)"
trap 'rm -rf "$WORK"' EXIT
CODE="$WORK/snippet.cs"
sed -e "s|__BUNDLE__|$BUNDLE|g" -e "s|__SERVER_DIR__|$SERVER_DIR|g" "$SNIPPET" > "$CODE"

uloop execute-dynamic-code --project-path "$PROJECT" --code-file "$CODE"
