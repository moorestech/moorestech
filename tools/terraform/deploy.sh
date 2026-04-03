#!/bin/bash
set -euo pipefail

# moorestech サーバーデプロイスクリプト
# Deploy moorestech server to EC2

HOST="$1"
KEY_FILE="$2"

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
BUILD_DIR="$SCRIPT_DIR/../moorestech_server/Output_DedicatedServer_StandaloneLinux64"
GAME_DATA_DIR="$SCRIPT_DIR/../../moorestech_master/server_v8"

SSH_OPTS="-o StrictHostKeyChecking=no -o UserKnownHostsFile=/dev/null -o ConnectTimeout=30"
SSH_CMD="ssh $SSH_OPTS -i $KEY_FILE ubuntu@$HOST"
SCP_CMD="scp $SSH_OPTS -i $KEY_FILE"

echo "=== Deploying moorestech server to $HOST ==="

# SSH接続待機（EC2起動直後はSSHが使えないことがある）
# Wait for SSH to become available
echo "Waiting for SSH connection..."
for i in $(seq 1 30); do
    if $SSH_CMD "echo ok" 2>/dev/null; then
        break
    fi
    echo "  Attempt $i/30..."
    sleep 5
done

# ビルド成果物の転送
# Transfer build artifacts
echo "Transferring server build..."
$SCP_CMD -r "$BUILD_DIR/." "ubuntu@$HOST:~/moorestech_server/"

# ゲームデータの転送（mods, map, config）
# Transfer game data (mods, map, config)
if [ -d "$GAME_DATA_DIR" ]; then
    echo "Transferring game data..."
    $SCP_CMD -r "$GAME_DATA_DIR/." "ubuntu@$HOST:~/game/"
else
    echo "WARNING: Game data directory not found at $GAME_DATA_DIR, skipping."
fi

# サーバー起動
# Start the server
echo "Starting moorestech server..."
$SSH_CMD << 'REMOTE_SCRIPT'
    # 既存プロセスを停止
    # Stop existing server process if running
    pkill -f moorestech_server || true

    # 実行権限を付与
    # Grant execute permission
    chmod +x ~/moorestech_server/moorestech_server

    # バックグラウンドでサーバー起動（デフォルトで ../../game を参照）
    # Start server in background (defaults to ../../game relative to dataPath)
    cd ~/moorestech_server
    nohup ./moorestech_server \
        > ~/moorestech_server.log 2>&1 &

    echo "Server started (PID: $!)"
    sleep 2

    # 起動確認
    # Verify server is running
    if pgrep -f moorestech_server > /dev/null; then
        echo "Server is running successfully."
    else
        echo "ERROR: Server failed to start. Log output:"
        tail -20 ~/moorestech_server.log
        exit 1
    fi
REMOTE_SCRIPT

echo ""
echo "=== Deployment complete ==="
echo "Server: $HOST:11564"
echo "SSH: ssh -i $KEY_FILE ubuntu@$HOST"
echo "Logs: ssh -i $KEY_FILE ubuntu@$HOST 'tail -f ~/moorestech_server.log'"
