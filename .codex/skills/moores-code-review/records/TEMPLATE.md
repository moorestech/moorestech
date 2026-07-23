# <topic> レビュー記録 (YYYY-MM-DD)

<!-- 1レビュー実行=1ファイル。命名: YYYY-MM-DD-<topic>.md（再レビューは -r2 付き新ファイル＋相互リンク1行）。
     記録は不変。マージ後に判明した事実のみ「事後結果」へ追記可。設計根拠: docs/superpowers/specs/2026-07-23-review-records-design.md -->
<!-- One review run = one immutable file; only the post-merge outcome section may be appended later. -->

## 対象
- base: `<sha>` / reviewed head: `<sha>`（dirty込みの場合はその旨＋`git diff --stat`要約を注記）
- ブランチ: / PR:（あれば）
- context要約 — ゴール: / 非目標: / 許容トレードオフ: / 制約:

## 系統別判定
| 系統 | Critical | 要旨 |
|---|---|---|
| 決定論チェック | | |
| <レンズ/reviewer名…> | | |
| Codex外部監査 | | |
| Fable全般 | | |

## 適用した修正
- <修正1行（出所系統）> → 適用コミット `<sha>`

## 設計判断（AskUserQuestion裁定）
- Q: <問い> / 選択肢: <…> / 裁定: <ユーザー回答> / 適用: <結果・コミット>

## 破棄した指摘
- <指摘> — 破棄理由（実コード照合の結果等）

## 事後結果（マージ後追記可）
- <人間レビュー指摘・裁定の覆り・ハーネス改修など>

## メタ
- セッションID: / スキップ系統:（codex不在等） / 備考:
