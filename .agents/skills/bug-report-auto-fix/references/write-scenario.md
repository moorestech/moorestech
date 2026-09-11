# 追加の観察シナリオを書く（最大3本）

`scripts/scenarios/bug-report-observe.cs`（報告者の位置で30秒ただ見る）で症状が出なかったときに、
**説明文が述べている操作を自分で再現する**シナリオを書き足す。上限は3本（HARD GATE の `not_reproduced` 分岐）。

DSL の書き方・API 一覧・実行方法の正本は `.agents/skills/unity-playmode-recorded-playtest/`
（`references/write-scenario.md`・`references/place-blocks-via-ui.md`・`references/hotbar-driven-systems.md`・
`references/troubleshooting.md`）。ここには**バグ報告バンドルから走らせるとき固有の差分**だけを書く。

## バンドル固有の作法

- **ワールドは必ずバンドルのものを使う**。`PLAYTEST_WORLD_DIRECTORY=$WORLD_DIR`（`run.env`）を渡す。
  `world.json` の `mapMode` が `template` なら `PLAYTEST_MAP_MODE=template PLAYTEST_SEED=0`、
  `generated` なら `PLAYTEST_MAP_MODE=generated PLAYTEST_SEED=<world.jsonのseed>`。
  シードを取り違えると地形が変わり、症状が出ないのはバグが無いからではなくワールドが違うからになる
- **master data は `$MASTER_DIR`**（報告時の実チェックアウト値で切られた worktree）。
  別の master を渡すとスキーマ不整合で `MooresmasterLoaderException` により初期化が無言で死ぬ
- `__BUNDLE__` プレースホルダを `sed "s|__BUNDLE__|$RUN|g"` で実値へ置換してから渡す（`run-scenario.sh` は置換しない）
- シナリオは `$RUN/` 側へ書き出す（`$RUN/observe-2.cs` 等）。**`$WORKTREE` のコードrepoへコミットしない** —
  バンドル依存のシナリオは証拠であって、PR に載る再現テストではない（裁定 2026-09-11）
- 結果ディレクトリは `$RUN/observe-2/` のように連番で残す。上書きすると「何を試したか」が消え、
  `not_reproduced` の `summary` に書くべき材料が無くなる

## 3本の使い分け（症状が出ないときの当て方）

1. **説明文の操作をそのまま**: 説明文の動詞（置いた・繋いだ・開いた・走らせた）を DSL の操作に写す。
   `$RUN/packets.jsonl` の末尾数十行が報告直前に実際に飛んだ操作なので、そこから操作列を起こすのが最短
2. **時間を伸ばす**: 症状が蓄積型（搬送の詰まり・在庫の増減・列車の周回）なら観察を 30 秒→180 秒へ伸ばす。
   待ちは `await p.WaitSeconds(...)` で、ゲームロジック側の経過は tick で見る
3. **視点と場所を変える**: 報告者の位置から見えていないだけのことがある。`p.WarpPlayer(...)` で対象の
   ブロック・駅・機械の正面へ寄り、`p.Screenshot(...)` を細かく刻む

各シナリオに `p.Note("狙い: ...")` を先頭へ置く。動画左上と `result.json` の `Timeline` に残り、
`not_reproduced` で終えたときに「何を試したか」の証拠がそのまま残る。

## 症状を機械的に判定する

目視だけに頼らず、出るはずの異常を `p.Assert(...)` で書く（`Assert` は失敗しても続行し `result.json` に残る）。
例外・エラーログは `result.json` の `ErrorLogs` に集まるので、シナリオ側で拾い直さなくてよい。

症状が出たら、その `Assert` がそのまま **Step 5 の合成NUnit の期待値の元**になる。
ただし合成NUnit はワールドファイルに依存しない最小構成へ書き直すこと — シナリオをそのままテストにしない。
