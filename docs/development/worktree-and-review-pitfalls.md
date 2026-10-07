# worktree・Web UI・レビュー工程の落とし穴

新しい worktree で作業を始めるとき、Web UI のテストが不安定なとき、moores-code-review の自動適用を受けたときに読む。
Mac mini の `moores-wt` 固有の癖は `docs/development/macmini/worktree-and-automation.md`。

## master ピン（`.moorestech-external-revisions.json`）が勝手に戻る
- Unity Editor が起動していると、ピンを書き換えても Editor が実チェックアウト値へ書き戻す。`git add -A` で巻き込むとピン更新が無言で取り消され、計測やテストが意図と違うマスタで走る
- 規則: ピン変更は `git add .moorestech-external-revisions.json` 単独でコミットし、直後に `git show HEAD:.moorestech-external-revisions.json` で値を確かめる
- playtest でマスタを固定したいときは、`run-scenario.sh <project> <scenario> <master-server-dir>` の第3引数で明示し、ピンに依存させない

## Web UI（`moorestech_web/webui`）
### node_modules の配備
- webui は pnpm 管理（`.npmrc` に `node-linker=hoisted`）。新しい worktree では `cd moorestech_web/webui && pnpm install --frozen-lockfile` で配備する
- 別 checkout の node_modules を `cp -Rc` で持ち込むと、vitest は起動しても unit/e2e の実行時に異常が出る。`npm ci` は `Unknown project config node-linker` で失敗する
- 壊れたら `rm -rf node_modules && pnpm install`

### e2e が実行ごとに違う spec で落ちる
- `npm run test:e2e` は playwright の webServer / mock-host が **固定ポート 5273** を使う（`e2e/playwright.config.ts`）。同じマシンで別セッションが e2e を走らせていると衝突する
- 分かりやすい形: `Error: http://localhost:5273 is already used` で即死
- 厄介な形: 自分のテストが他セッションの mock-host に繋がり、別の fixture を読んで落ちる。落ちる spec が実行ごとに変わり、自分の変更の回帰に見える
- 判別: 疑わしい spec を単独で3回連続実行して緑ならポート衝突。`lsof -ti :5273` に自分が起動していない PID が出れば確定
- 対処: 待機と実行を1コマンドに繋げてバックグラウンドで走らせる。分けるとその隙にポートを取り返される
  `until ! lsof -ti :5273 >/dev/null 2>&1; do sleep 5; done && npm run test:e2e`
- 回帰の切り分けで revert 実験をする前に、まずポートを疑う

## 新しい worktree で録画シナリオ（run-scenario.sh）が ready 後に止まる
どれも「ready 後に無言で止まる」形で出て、シナリオの不具合に見える。run-scenario.sh の前に 1・2 を用意する。
1. 同梱 Node（`moorestech_web/node`、gitignore）が無く WebUiHost が起動しない（ログ `Node binary not found`）→ `moorestech_web/setup.sh` を実行するか、既存 checkout から `cp -Rc` する
2. `moorestech_web/webui/node_modules` が無い → 上記のとおり `pnpm install --frozen-lockfile`
3. PlayMode 中に `Time.timeScale` が 0 のままで、scaled な `UniTask.Delay`（`PlaytestDriver.WaitSeconds` 等）が返らず result.json 未出力のままタイムアウトする。Game View が Scene View の裏タブだと Recorder が初回フレーム待ちで timeScale=0 に固定する。Game View を表に出す。止まったら `uloop execute-dynamic-code` で `Time.timeScale` や `IsPlayingSkit` を覗く

## moores-code-review Workflow の自動適用後はテストを再実行する
- Workflow（`review_workflow.js`）の Apply フェーズは `uloop compile` までしか確かめない（返り値 `apply.tests` が「実行せず」）。前例に揃えた適用でも、既存テストが固定している節度（発火回数など）と衝突しうる
- 規則: Workflow の完了通知を受けたら、報告や AskUserQuestion の前に対象テスト（Unity EditMode の regex 指定分と webui の unit/e2e）を再実行する。赤なら適用内容を最小修正してから記録・PR に進む
