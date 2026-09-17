# プレイテスト運用スクリプト（Mac mini）

受け口 Worker は `tools/playtest-receiver/`。ここには Mac mini 側から叩く運用スクリプトを置く。

## 設定

`~/hermes-agent/data/services/playtest/env.sh`（git 管理外・実シークレット）:
```
export PLAYTEST_RECEIVER_BASE=https://playtest.tar-atari.com
export PLAYTEST_ADMIN_KEY=<wrangler secret put ADMIN_KEY で入れたのと同じ値>
```
別の場所に置く場合は `PLAYTEST_ENV_FILE` で指す。

## 許可リスト

```bash
scripts/playtest/allowlist.sh list
scripts/playtest/allowlist.sh add 76561198000000001
scripts/playtest/allowlist.sh remove 76561198000000001
```

許可リストは全置換 PUT で更新する。`add`/`remove` は内部で GET → 編集 → PUT を行うため、
2人が同時に実行すると後勝ちで片方の変更が消える。人手運用なので排他は設けていない。

不許可にした瞬間から新しいセッショントークンは出なくなるが、発行済みトークンは最大1時間有効で、
その間はアップロードだけ通る。起動時照合（クライアント）は次回起動から効く。

## 配布工程

配布ビルドを焼き、Steam の `playtest` ブランチへ上げ、検証機で通し検証するまでの運用。
用語は CONTEXT.md「プレイテスト」節、裁定は docs/adr/0061 を正とする。

### 1回だけ行う準備

#### Steamworks 側（Web の手動作業。自動化しない）

1. アプリ 1958160 の Steamworks 管理画面 → SteamPipe → Builds でベータブランチ `playtest` を作成する。
2. `playtest` ブランチにパスワードを設定する（テスターへキーと一緒に配る）。
3. Depot のIDを控える（Steamworks → SteamPipe → Depots）。`MOORESTECH_STEAM_DEPOT_ID` に設定する。
4. Steam Web API の publisher key を発行する（受け口 plan D の `STEAM_WEB_API_KEY` に使う）。
5. テスター配布用のキーを発行する（Steamworks → Packages → キー生成）。

#### Mac mini 側

1. steamcmd を入れる: `brew install --cask steamcmd`（`steamcmd` が PATH に載る）。
2. 初回だけ対話で Steam Guard を通す: `steamcmd +login <user> +quit`（以降は保存された資格で無人ログインできる）。
3. `~/hermes-agent/data/services/playtest/env.sh` に次を追記して export する（このファイルは封じ込め env の外に置かず、値をログへ出さない）:
   - `MOORESTECH_STEAM_USER`
   - `MOORESTECH_STEAM_DEPOT_ID`
   - 検証機向けの変数（Task 6 の節を参照）

### 使い方

```bash
. ~/hermes-agent/data/services/playtest/env.sh
scripts/playtest/release-playtest.sh <master のコミット>
```

成果物・ログ・告知テキストは `~/hermes-agent/data/services/playtest/runs/<label>/` に残る。

## テスト

```bash
bash scripts/playtest/tests/test-allowlist.sh          # OK と出れば合格
bash scripts/playtest/tests/release-playtest-test.sh    # PASS: release-playtest contract と出れば合格
```
