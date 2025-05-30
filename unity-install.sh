#!/usr/bin/env bash
set -euo pipefail

# ────────────────────────────────────────────────────────────────
# 1) 必須環境変数の確認
# ────────────────────────────────────────────────────────────────
: "${UNITY_EMAIL:?Error: UNITY_EMAIL is not set}"
: "${UNITY_PASSWORD:?Error: UNITY_PASSWORD is not set}"
: "${UNITY_VERSION:?Error: UNITY_VERSION is not set}"

# 任意設定
UNITY_MODULES=${UNITY_MODULES:-""}          # 例: "android webgl"
UNITY_SERIAL=${UNITY_SERIAL:-""}            # Pro/Plus シリアル (Personal なら空で OK)
UNITY_INSTALL_PATH=${UNITY_INSTALL_PATH:-"/opt/unity"}

# ────────────────────────────────────────────────────────────────
# 2) Unity Hub のインストール（未インストールの場合のみ）
#    公式 apt リポジトリを登録し、unityhub パッケージを導入
# ────────────────────────────────────────────────────────────────
if ! command -v unityhub &>/dev/null; then
  echo ">> Installing Unity Hub ..."
  sudo apt-get update
  sudo apt-get install -y wget gpg ca-certificates

  # GPG キー & リポジトリ定義（Ubuntu 22.04/24.04 以降の deb822 形式）
  wget -qO - https://hub.unity3d.com/linux/keys/public \
    | gpg --dearmor \
    | sudo tee /usr/share/keyrings/Unity_Technologies_ApS.gpg >/dev/null

  cat <<'EOF' | sudo tee /etc/apt/sources.list.d/unityhub.sources >/dev/null
Types: deb
URIs: https://hub.unity3d.com/linux/repos/deb
Suites: stable
Components: main
Signed-By: /usr/share/keyrings/Unity_Technologies_ApS.gpg
EOF

  sudo apt-get update
  sudo apt-get install -y unityhub
fi

# ────────────────────────────────────────────────────────────────
# 3) EULA 自動同意 (GUI が無い環境向け)
# ────────────────────────────────────────────────────────────────
mkdir -p "$HOME/.config/Unity Hub"
echo '{"accepted":[{"version":"3"}]}' >"$HOME/.config/Unity Hub/eulaAccepted"

# ────────────────────────────────────────────────────────────────
# 4) 指定バージョンの Unity Editor をインストール
#    Unity Hub CLI (headless) を利用
# ────────────────────────────────────────────────────────────────
echo ">> Installing Unity Editor $UNITY_VERSION ..."
args=(--headless install --version "$UNITY_VERSION")
if [[ -n "$UNITY_MODULES" ]]; then
  for m in $UNITY_MODULES; do
    args+=( -m "$m" )
  done
fi
unityhub "${args[@]}"

# ────────────────────────────────────────────────────────────────
# 5) (オプション) ライセンス自動アクティベーション
#    Personal 版はシリアルが無いのでスキップしてください
# ────────────────────────────────────────────────────────────────
if [[ -n "$UNITY_SERIAL" ]]; then
  # Editor 実行ファイルのパス (Hub の既定配置を想定)
  EDITOR="$HOME/Unity/Hub/Editor/$UNITY_VERSION/Editor/Unity"
  [[ -x $EDITOR ]] || EDITOR="$UNITY_INSTALL_PATH/Hub/Editor/$UNITY_VERSION/Editor/Unity"
  if [[ -x $EDITOR ]]; then
    echo ">> Activating license ..."
    "$EDITOR" -quit -batchmode -nographics \
      -serial "$UNITY_SERIAL" \
      -username "$UNITY_EMAIL" \
      -password "$UNITY_PASSWORD" || true
  else
    echo "!! Editor binary not found – licence activation skipped."
  fi
else
  echo ">> UNITY_SERIAL not set – licence activation skipped (Personal licence assumed)."
fi

echo "✅ Unity $UNITY_VERSION installation finished."
