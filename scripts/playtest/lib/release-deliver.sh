#!/usr/bin/env bash
# 検査を通った build-windows / build-mac を Steam の playtest-staging へ上げ、本体を消し、検証機で通し検証して反映手順を書く（焼き方に依らず共有）
# Uploads the inspected build-windows / build-mac to Steam playtest-staging, removes the payloads, runs the check machine and writes the promotion steps (shared by every bake route)

release_deliver() {
    release_render_steam_vdfs "$RUN_DIR" "$STEAM_DIR"

    python3 "$SCRIPT_DIR/steam_auth/owned_process.py" "$STEAMCMD_BIN" +login "$MOORESTECH_STEAM_USER" +run_app_build "$STEAM_DIR/app_build_playtest.vdf" +quit

    # Steamに格納済みの本体だけを消し、調査ログと検証資料は残す
    # Remove only uploaded build payloads, keeping logs and verification evidence
    if rm -rf -- "$RUN_DIR/build-windows" "$RUN_DIR/build-mac"; then
        echo "[release-playtest] uploaded build payloads removed: $RUN_DIR/build-windows $RUN_DIR/build-mac"
    else
        echo "WARNING: アップロード済みビルド本体の削除に失敗しました" >&2
        for build_dir in "$RUN_DIR/build-windows" "$RUN_DIR/build-mac"; do
            [ ! -e "$build_dir" ] && [ ! -L "$build_dir" ] || echo "WARNING: 残存パス: $build_dir" >&2
        done
    fi

    # 検証機の通し検証に通ったものだけを手動反映の対象にする
    # Only a build that passed the check machine becomes eligible for manual promotion
    "$VERIFY_SCRIPT" "$BUILD_LABEL"

    cat >"$RUN_DIR/promotion.md" <<EOF
# moorestech プレイテスト反映手順 ($BUILD_LABEL)

- コミット: $COMMIT_FULL
- Steam アップロード先: playtest-staging（Windows depot と Mac depot を同じビルドに格納）
- 通し検証: Windows は検証機で phase1 / phase2 とも合格。Mac 版は自動検証していない（ADR 0071）

## Mac 版の手動確認（playtest 反映前に必須）

1. Apple Silicon の Mac で Steam のベータ \`playtest-staging\` を選び、更新後に Steam から起動する
2. タイトルから新規ワールドへ入り、Tab でインベントリが開くことを確認する
3. 起動に回避操作（セキュリティ設定の変更・コマンド実行）が要ったか: [ ] 要らなかった / [ ] 要った（手順: ）
   要った場合も配布は止めず、その手順をキーと一緒にテスターへ案内する

## 反映

Steamworks → アプリ 1958160 → SteamPipe → ビルドで、検証済みビルドを \`playtest\` ブランチに手動でライブ設定してください。設定後に対象のビルド ID を確認し、テスターへ告知してください。
EOF
    echo "[release-playtest] promotion: $RUN_DIR/promotion.md"
}
