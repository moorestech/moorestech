# ADR 0104: 削除ツールの結果表示は種類と理由ごとの件数にまとめ、ベルト撤去の返却物は対象の区間だけを読む

- Status: Accepted
- Date: 2026-11-19
- Related: ADR 0091（ブロック状態の束ね送信。変えない）

## Context

削除ツールで数百件をまとめて撤去すると、確定の瞬間に画面が秒単位で固まるという報告があった（ユーザー実測: 416件で約16秒）。

- 原因: 実測（証拠: プレイモードで96マスのベルト＋チェスト計97件を箱選択→F長押しで撤去。最悪フレーム 658.5ms のうち `LayoutUpdater.DispatchChangeEvents` が 610.4ms、その中の `TextGenerator.CalculatePreferredValues` が1パスあたり 9,506回＝97×98。アイテム無しの対照でも 772ms＋433ms で同じ。計測シナリオは `moorestech_client/PlaytestResults/scratch/belt-bulk-delete-with-items.cs`、プロファイラ有効下の値）
  - 削除を確定すると `DeleteResultFeedback` が応答待ちの対象1件につき1行をカーソルツールチップへ足す。
  - `TooltipFace.SetLines` は行ごとに `GeometryChangedEvent` を登録し、そのたびに全行の文字幅を測り直す。行数 N に対して N×(N+1) 回の計測になる。
- 別件の原因: 実測（証拠: 一時テストで `RemoveBlockProtocol` 経由の撤去時間を計測。ベルト1万マス・アイテム1万個のワールドで500マス撤去が 1,966ms、同じワールドでアイテム0個なら 26.7ms）
  - `BeltTransportDatastore.GetOwnedRefundItems` と `GetConnectionRemovalRefundItems` が、撤去1個ごとに組の全量スナップショットを1回ずつ取っていた。

## Decision

### 1. 応答待ちは「処理中 N件」の1行にまとめる

- 出所: ユーザー裁定 2026-11-19 原文「ツールチップを「処理中 N件」の1行にまとめれば、この16秒の大半は消えるはずです。であればやってほしい」

### 2. 拒否・未確認は理由ごとに件数の1組へまとめ、通知も理由ごとに1通にする

- ツールチップは「種類＋理由」ごとに、件数の行と理由の行を1組出す。画面から個々のブロック名と座標は消え、対象ごとの内訳は `DeleteCommitBatch` の開発者ログに残る。
- 出所: ユーザー裁定 2026-11-19 質問「拒否・未確認の表示をどうしますか？」→ 選択「理由ごとに1行へまとめる」
- 件数と理由を1行に入れず2行に分けるのは agent前提（`LocalizationTextParam` が文字列しか差し込めず、翻訳済みの理由文を別の文へ埋め込む仕組みが無いため。従来も対象の行と理由の行は別だった）。
- 通知文から対象名を外す（`ui.notification.deleteRejected`・`deleteUnconfirmed`）。出所: agent前提（上の裁定の帰結。1通が複数対象を表すため）。

### 3. ツールチップの折り返し判定は、形が変わった行だけを測る

- `TooltipFace` は行ごとの自然幅と「最大幅を超える行の数」を持ち、行の `GeometryChangedEvent` ではその行だけを測り直す。面の幅が変わったときは測り済みの幅を比べ直すだけにする。見た目は変えない。
- 出所: ユーザー裁定 2026-11-19 原文「そもそも何かの処理が二重ループになっていればだめなのでコードみなおしもついでにして」

### 4. ベルト撤去の返却物は、対象のblockが属するsegmentと自分の内部segmentだけを読む

- 返却される中身と並び順は変えない（走行中→buffer→内部segmentの順、内部segmentはsegment番号順）。
- 出所: ユーザー裁定 2026-11-19 質問「サーバー側の重さも今回一緒に直しますか？」→ 選択「今回一緒に直す」

## 手続きの省略

- moores-grill-with-docs §1.7（Codex 独立質問の消し込み）と moores-writing-plans への接続は行っていない。作業端末に個人スキル `codex-audit`・`grilling`・`domain-modeling` が無く実行できなかった。
- 出所: ユーザー裁定 2026-11-19 質問「この工程を省略して進めてよいですか？」→ 選択「省略して進める」

## 見直しで見つけ、今回は変えていないもの

いずれもコードの読みだけで、重さは測っていない（原因: 推測）。

- `BlockAttachedConnectionResolver.RefreshRailTargets` / `RefreshLineTargets`: レール・線の増減1回ごとに、赤表示を要求中の全ブロックを回る。選択中に数百ブロックへレールや線が付いていると、増減回数×選択数になる。
- `DeleteBoxTargetResolver.Resolve`: 箱の終点待ちの間、ワールドの全ブロックを走査する（呼ばれる頻度は未確認）。
- `RemovedObjectCollector.MergeFrom` / `AddUnrecordableBlock`: 記録不能ブロックの重複判定が線形探索。記録不能が大量に出たときだけ2乗になる。
- ブロックの消滅アニメーション（`RendererShaderAnimation.RemoveAnimation`）: 1個につきトゥイーン2本。件数に比例するだけで2乗ではない。
