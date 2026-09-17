# プレイテスト運用スクリプト（Mac mini）

受け口 Worker は `tools/playtest-receiver/`。ここには Mac mini 側から叩く運用スクリプトを置く。

## 設定

`/Users/sakastudio/hermes-agent/data/services/playtest/env.sh`（git 管理外・実シークレット。既定は本体 clone の位置から `<repo>/../../services/playtest/env.sh` と導出する）:
```
export PLAYTEST_RECEIVER_BASE=https://playtest.tar-atari.com
export PLAYTEST_ADMIN_KEY=<wrangler secret put ADMIN_KEY で入れたのと同じ値>
```
別の場所に置く場合は `PLAYTEST_ENV_FILE` で指す。worktree から叩くと兄弟パスがずれるので本体 clone のスクリプトを使う。受け口 admin API の呼び出しは `lib/receiver-api.sh` に一本化している。

## 許可リスト

```bash
bash /Users/sakastudio/hermes-agent/data/repos/moorestech/scripts/playtest/allowlist.sh list
bash /Users/sakastudio/hermes-agent/data/repos/moorestech/scripts/playtest/allowlist.sh add 76561198000000001
bash /Users/sakastudio/hermes-agent/data/repos/moorestech/scripts/playtest/allowlist.sh remove 76561198000000001
```

許可リストは全置換 PUT で更新する。`add`/`remove` は内部で GET → 編集 → PUT を行うため、
2人が同時に実行すると後勝ちで片方の変更が消える。人手運用なので排他は設けていない。

不許可にした瞬間から新しいセッショントークンは出なくなるが、発行済みトークンは最大1時間有効で、
その間はアップロードだけ通る。起動時照合（クライアント）は次回起動から効く。

## テスト

```bash
bash scripts/playtest/tests/test-allowlist.sh   # OK と出れば合格
```
