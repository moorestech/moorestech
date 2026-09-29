#!/usr/bin/env bash
# 接続の組み立てだけを共有する。起動・認証鍵の準備は呼び出し元の責務
# Share only SSH connection assembly; boot and application credentials belong to callers
verify_ssh() {
    if [ -z "${MOORESTECH_VERIFY_HOST:-}" ] || [ -z "${MOORESTECH_VERIFY_USER:-}" ]; then
        echo 'ERROR: MOORESTECH_VERIFY_HOST / MOORESTECH_VERIFY_USER が必要です' >&2
        return 2
    fi
    "${SSH_BIN:-ssh}" -o BatchMode=yes "$@"
}

verify_ssh_remote() {
    verify_ssh "${MOORESTECH_VERIFY_USER:-}@${MOORESTECH_VERIFY_HOST:-}" "$@"
}
