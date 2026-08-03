# PR #1095 Web UI fixture CI修正 レビュー記録 (2026-08-01)

<!-- 1レビュー実行=1ファイル。命名: YYYY-MM-DD-<topic>.md（再レビューは -r2 付き新ファイル＋相互リンク1行）。
     記録は不変。マージ後に判明した事実のみ「事後結果」へ追記可。設計根拠: docs/superpowers/specs/2026-07-23-review-records-design.md -->

## 対象
- base: `6523c6e8615dbd4b35db60463b0cb1e881124e3c` / reviewed head: 同SHAのdirty差分（`fixtures.ts` 1ファイル、2 insertions）
- ブランチ: `feature/placement-guid-equipment-mining` / PR: `#1095`
- context要約 — ゴール: E2E inventory fixtureの必須confirmation revision欠落を修正してWeb UI CIを復旧 / 非目標: production同期ロジック・契約スキーマ・外部revisionの変更 / 許容トレードオフ: なし / 制約: 初期revisionは既存fixtureと同じ`0`、既存dirty差分を除外、CI相当検証

## 系統別判定
| 系統 | Critical | 要旨 |
|---|---|---|
| 決定論チェック | 0 | confirmed・全候補とも0件 |
| precedent-alignment | 0 | 既存の`PlayerInventoryData`テストfixtureと同じ初期値`0`に一致 |
| 汎用reviewer 7観点 | 0 | 依頼達成、配置、重複、デッドコード、結果伝播、SSOTに問題なし |
| Codex外部監査 | 0 | 通常・デモfixtureの両方を確認し、スキーマ・初期状態・mock挙動に問題なし |
| Fable全般 | 0 | 必須フィールド追従として妥当、外部revision変更はパッチ対象外 |
| コメントpost-check | 0 | 根拠喪失・コメント規約違反ともになし |

## 適用した修正
- `inventory`と`demoInventory`へ`equipmentSelectionConfirmationRevision: 0`を追加（CIログ・ユーザー依頼） → 本記録と同一コミット

## 設計判断（AskUserQuestion裁定）
- なし

## 破棄した指摘
- なし

## 事後結果（マージ後追記可）

## メタ
- セッションID: 現Codex修正セッション / スキップ系統: なし（指定モデル名は環境非提供のため利用可能な高精度モデルで代替） / 備考: E2E型チェック、`tsc -b --force`、Vitest 426件、`git diff --check`を実施。suppressed: 0件
