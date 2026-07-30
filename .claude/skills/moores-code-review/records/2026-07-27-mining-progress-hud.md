# 採掘進捗HUD統合 レビュー記録 (2026-07-27)

## 対象

- base: `2cf3078f0e12215952ca74ecd0562d5620027567` / reviewed head: `952aa44f3`（レビュー中のdirty差分を含めて監査後、同SHAへ確定）
- ブランチ: `fix/mining-progress-hud` / PR: なし
- context要約 — ゴール: 採掘対象名と重複HUDを撤去し、単一GaugeBarをホットバーへ整列 / 非目標: 採掘ロジックとホットバー操作の再設計 / 許容トレードオフ: `ui.mining_hud`の後方互換性を維持しない / 制約: C#コンパイル・対象Unityテスト・Playwright実測

## 系統別判定

| 系統 | Critical | 要旨 |
|---|---|---|
| 決定論チェック | 0 | 禁止構文・比較演算子違反0。既存2ファイルの205行超過は努力目標Warning |
| domain-boundary / precedent-alignment | 0 | 既存イベント駆動`ui.progress`への一本化と共通GaugeBar利用は前例一致 |
| 汎用C# reviewer群 | 2 | 旧Topic専用`GetProgress()`と未使用usingを検出し削除 |
| 汎用TS reviewer群 | 0 | SSOT・dev分離・結果伝播・デッドコードに違反なし |
| test-mutation / implicit-value | 0 | クロップ余白の裸値を命名。Unity実入力からTopicまでの結合テスト不足はWarning |
| Codex外部監査 | 0 Critical / 3 Medium / 3 Low | 更新後非表示・縮小viewport・全E2E証跡、汎用トークン名、C#デッドコードを改善 |
| Fable全般（利用可能モデルへ縮退） | 0 | 負のTopic契約テスト、古い資料2件、capture名不一致を検出し修正 |
| comment-rationale-guard | 0 | load-bearingな根拠コメントの不当削除なし |
| comment-convention-guard | 8 | 機械的短縮8件を適用、根拠保全5件を残置、名前重複0 |

## 適用した修正

- 旧採掘Topic専用の`GetProgress()`と未使用usingを削除（C# dead-code reviewer / Codex）
- `--mining-progress-hotbar-gap`を汎用`--progress-hotbar-gap`へ改名（Codex）
- 進捗0.2→1.0→非表示と960×540縮小配置のPlaywright回帰を追加（Codex）
- `ui.mining_hud`非公開の負のwire契約テストを追加（Fable）
- `docs/webui`の旧Topic記述とcapture出力名を正本へ同期（Fable）
- capture余白を名前付き定数へ変更（implicit-value reviewer）
- コメント8組を意図を保って短縮（comment-convention-guard）
- 以上を適用コミット `952aa44f3`

## 設計判断（AskUserQuestion裁定）

- なし

## Warning / Info

- Warning: `fixtures.ts`と`validators.test.ts`は205行。既存ファイルの努力目標として残置し、今回のHUD変更で分割しない。
- Warning: `MapObjectMiningMiningState → ProgressBarView → ProgressTopic`の完全な実入力結合テストは未整備。製品経路は実コード照合し、Topic復元・wire・表示は各境界で検証済み。
- Info: 最終実測は幅差0.0625px、中心差0px、番号タブ間隔12px、バー1本、対象名0件。
- suppressed: 0件

## 破棄した指摘

- Codexの「最終Playwright証跡は8件のみ」は、その後の最終全実行 `111 passed` で解消・否定。

## 事後結果（マージ後追記可）

- なし

## メタ

- セッションID: Codex外部監査 `019fa34d-0387-7751-9c5c-24952494c5f0`
- スキップ系統: 指定opus/sonnet/fableモデルは実行環境で直接選択不能のため、利用可能な`gpt-5.6-sol`へ縮退して全観点を回収
- 備考: lint、Vitest 380件、Playwright 111件、Unity対象7件、Unity compile Error 0。uLoop起動時のSetupWizardWindow既知エラーのみ確認
