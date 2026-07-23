# 建築UI Ctrl+Zアンドゥ レビュー記録 (2026-07-22)

<!-- 初号記録。実行当時の一時ファイルは削除済みのため、セッションログから2026-07-23に遡及作成 -->
<!-- Retroactively reconstructed from session logs on 2026-07-23 (the original run predates the records mechanism) -->

## 対象
- base: `353c29f50` / reviewed head: `c628856aa`（クリーンツリー・コミット済み範囲、2121行）
- ブランチ: feature/build-undo-ctrl-z / PR: #1045（マージ: `2f42f7260`）
- context要約 — ゴール: 建築UI中のCtrl+Z/Cmd+Zで直前の設置/撤去バッチをクライアント完結で取り消す（逆操作プロトコル発行・照合ガード付き）/ 非目標: Redo・履歴永続化・サーバー側基盤 / 許容トレードオフ: 楽観記録＋照合ガード・ガード評価1回・try-finally境界例外 / 制約: AGENTS.md全規約・UIステート駆動規約

## 系統別判定
| 系統 | Critical | 要旨 |
|---|---|---|
| 決定論チェック | あり | comparison_operator候補3件→verifier送り |
| comparison-operator-verifier | あり | 比較演算子3件を機械的書き換え |
| core-cs-architecture-lifecycle | **あり** | **型switch→IBuildOperationRecordへExecuteUndo多態化**（後述の裁定へ） |
| core-cs-region-internal | あり | UndoAsyncの単一参照helper構造 → 裁定却下 |
| comment-convention-guard | あり | 要判断コメント短縮候補（(a)裁定へ） |
| type-driven-structure | なし | 型switchを「BlueprintRequest前例の正解形」と判定（**誤判定** — DTO前例をドメイン型へ誤適用。事後結果参照） |
| precedent-alignment | なし | 型switchを備考落ち（「spec記載・合意済み扱い」— **合意の出所が自作specだった**） |
| caller-orchestration-minimization | なし | CommitDelete戻り値の後続副作用を例外条項で容認（**見逃し**。事後結果参照） |
| fable-holistic-review | なし | DI配線・記録フック網羅性・照合ガード等を裏取り済み |
| dead-code-and-scope / centralization-duplication / result-state-propagation / unidirectional-flow / schema-design / bug-fix-intent / unity-convention / redundant-member-duplication / implicit-cardinality-assumption / core-any系4本 | なし | — |
| comment-rationale-guard | なし | 根拠コメント削除なし |
| Codex外部監査 | - | 出力は当時レビュー済み・ログ散逸（記録機構導入前のため） |

## 適用した修正
- 比較演算子3件・コメント機械的短縮（決定論/verifier/convention-guard）→ レビューセッション内で適用
- (a)裁定: 要判断コメント9件の短縮 → `5d2d2b3dd`
- (b)裁定: MainGameStarter 356行のDI分割 → `75f026457`（**事後revert** `0f89cd027`）

## 設計判断（AskUserQuestion裁定）
- Q1: BuildUndoServiceの型switch — architecture-lifecycleは多態化をCritical指摘、type-driven-structureは正反対に「正解形」判定 / 選択肢: 現状維持(推奨)・ポリモーフィズム化 / 裁定: **現状維持**（推奨に従う） / 適用: なし → **PRレビューで翻意**（事後結果参照）
- Q2: 保留3点 / 裁定: (a)コメント短縮を適用＋(b)既存200行超過も分割対応 / 適用: `5d2d2b3dd`・`75f026457`

## 破棄した指摘
- region-internal（UndoAsync構造）— AGENTS.mdの正例そのものの構造でdead-code-scope reviewerも逆判定のため裁定却下

## 事後結果（マージ後追記）
- 人間レビュー指摘7件（sakastudio・PR #1045）: ①DI分割revert（=Q2(b)裁定の覆り）→`0f89cd027` ②CommitDelete戻り値廃止 ③UndoAsync多態化（=Q1裁定の覆り）④RecordAndCommitDelete削除 →`96da28855` ⑤⑥Select系public削除 ⑦RemoveOperationRecord.CreateFrom化 →`5f1876cda`
- 根本原因: (1) type-driven-structureのDTO正解形をドメイン型へ誤適用→誤った「現状維持(推奨)」にユーザーがアンカー (2) caller-orchestrationの例外条項が移動先候補2択で却下 (3) 自作spec記載を「合意」扱いで備考落ち (4) 200行/コメント短縮質問が裁定疲れとスコープ外変更を誘発
- ハーネス改修（2026-07-23）: type-driven-structure基準5（振る舞い型switch多態化）・caller-orchestration基準4＋移動先判定手続き・precedent-alignment兄弟型対称基準・合意ガード3本・系統間矛盾時の推奨検証ルール・設計判断第3出口を追加。リプレイで指摘②〜⑦全件の検知を確認（fixture: pr1045 / pr1045-r2）。200行は努力目標化・コメント短縮質問は廃止（ユーザー裁定）
- 未対応: スコープ規律検知・非同期再入reviewer（eval/expected-findings.md #30/#31）

## メタ
- セッションID: 1f833ad9-04d9-41e2-b689-59907db704b4（レビュー実行）/ 68697493-ae3f-4184-96c8-018f9706a85e（遡及作成・見逃し分析）
- スキップ系統: なし（21 subagent＋決定論＋Codex実行）
