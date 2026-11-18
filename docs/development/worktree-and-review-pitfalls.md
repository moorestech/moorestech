# worktree・レビュー工程の落とし穴

新しい worktree で作業を始めるとき、moores-code-review の自動適用を受けたときに読む。
Mac mini の `moores-wt` 固有の癖は `docs/development/macmini/worktree-and-automation.md`。

## master ピン（`.moorestech-external-revisions.json`）が勝手に戻る
- Unity Editor が起動していると、ピンを書き換えても Editor が実チェックアウト値へ書き戻す。`git add -A` で巻き込むとピン更新が無言で取り消され、計測やテストが意図と違うマスタで走る
- 規則: ピン変更は `git add .moorestech-external-revisions.json` 単独でコミットし、直後に `git show HEAD:.moorestech-external-revisions.json` で値を確かめる
- playtest でマスタを固定したいときは、`run-scenario.sh <project> <scenario> <master-server-dir>` の第3引数で明示し、ピンに依存させない

## 新しい worktree で録画シナリオ（run-scenario.sh）が ready 後に止まる
「ready 後に無言で止まる」形で出て、シナリオの不具合に見える。Game View を表に出してから run-scenario.sh を流す。
- PlayMode 中に `Time.timeScale` が 0 のままで、scaled な `UniTask.Delay`（`PlaytestDriver.WaitSeconds` 等）が返らず result.json 未出力のままタイムアウトする。Game View が Scene View の裏タブだと Recorder が初回フレーム待ちで timeScale=0 に固定する。Game View を表に出す。止まったら `uloop execute-dynamic-code` で `Time.timeScale` や `IsPlayingSkit` を覗く

## moores-code-review Workflow の自動適用後はテストを再実行する
- Workflow（`review_workflow.js`）の Apply フェーズは `uloop compile` までしか確かめない（返り値 `apply.tests` が「実行せず」）。前例に揃えた適用でも、既存テストが固定している節度（発火回数など）と衝突しうる
- 規則: Workflow の完了通知を受けたら、報告や AskUserQuestion の前に対象テスト（Unity EditMode の regex 指定分）を再実行する。赤なら適用内容を最小修正してから記録・PR に進む
