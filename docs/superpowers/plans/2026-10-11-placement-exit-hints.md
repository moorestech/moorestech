# 設置・破壊モード終了案内 Implementation Plan

> このセッション内で実行するユーザー委任。moores-subagent-driven-developmentの単一subagent実装モードでTask 1を実行し、本体が末尾の検証・レビュー・PRを行う。新規統括セッションは作らない。

**Goal:** 設置・破壊モードで終了操作を先頭から読めるようにする。
**Architecture:** 既存C# UIStateのKeyHint配列とLocalization CSVを更新する。Web描画と入力解釈は既存経路を使う。
**Tech Stack:** C#、Unity、Localization CSV、既存Web UI HUD。

## Requirements

- 設置・破壊の終了案内を先頭へ配置する。B/Gの即終了と右短押し/Escの二段階解除を区別する。
- 右短押しはUI外であること、選択や起点があれば解除し、無ければ終了することを文言で伝える。
- CSVのSource/english/japanese/german/koreanを全て埋める。生成LocalizationKeysは手編集しない。
- 入力挙動、他画面の操作ヒント、HUDの色/構造は変更しない。
- コンパイル・対象テスト・unityプレイ録画テストで確認し、必須レビュー・push・PR作成まで完了する。

## Global Constraints

- 全コード200行未満、partial/Func禁止。.meta手作成禁止。
- この専用worktree以外へ実装しない。他タスクは引き受けない。
- バックグラウンドのポーリングで待たず全部前景で実行する／区切りごとにコミットする。
- 最終進捗は /tmp/moorestech-task-b-20261011.progress.md に記録する。

## 設計検査記録

- 配置検査（spec-architecture-review Phase 1〜2.5）: 実施済み / 違反0件 / ADR0032のUIState宣言と既存CSVの責務を維持。
- Phase 2.6（型閉包・重複・ADR矛盾）: 実施済み / 強0・弱0・第3バケツ0 / 独立検査で発火なし

## Task 1: 操作ヒントと全言語を揃える

Modify:
- moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/PlaceBlock/PlaceBlockStateHints.cs
- moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/DeleteObjectState.cs（ヒント配列のみ）
- Localization/localization.csv
- moorestech_client/Assets/Scripts/Client.Tests/UIState/UIStateKeyHintCatalogTest.cs（既存期待値が影響する場合のみ）

- [ ] CSVへ `ui.keyHint.key.rightShortPressOutsideUiOrEsc` と `ui.keyHint.text.cancelOperationOrExitMode` を追加する。日本語は「UI外で右クリック短押し / Esc」「選択・起点を解除 / なければモード終了」。英語・独語・韓国語も同じ意味にする。
- [ ] 設置はB終了を先頭、破壊はG終了を先頭に移し、その直後へ新案内を追加する。

```csharp
new KeyHint(LocalizationKeys.Ui.KeyHint.Key.RightShortPressOutsideUiOrEsc,
    LocalizationKeys.Ui.KeyHint.Text.CancelOperationOrExitMode),
```

- [ ] 既存カタログテストで設置/破壊のみEsc許容、終了の先頭配置、他画面は従来どおりを検証する。低影響のCSV文言を写すだけの新テストは増やさない。
- [ ] `uloop compile --project-path ./moorestech_client` を実行（ErrorCount 0）。対象テストは `UIStateKeyHintCatalogTest|RightShortPressInput.*Test` で絞りPASSを確認する。
- [ ] `Task 1: 設置と破壊モードの終了案内を全言語で追加` としてコミットする。

## Task 2: 実画面・二段階解除の検証

- [ ] 既存DSLで設置と破壊へ入力経由で遷移し、キーの表示順と翻訳済みの内容を確認する。
- [ ] Esc/右短押しで選択なし時に終了する既存挙動を確認する。可能なら選択保持時の解除を確認する。
- [ ] 動画と両モードの実画面スクリーンショットを保存・目視し、PR掲載用画像を docs/pr-assets/placement-exit-hints/ へコミットする。

## Task 3: 必須レビュー

- [ ] moores-code-reviewスキルで全ブランチレビューを前景実行する。Codexホスト用runnerを使い、系統の自己代行・縮退はしない。指摘があれば修正・再検証する。

## Task 4: PRと成果保全

- [ ] pr-createスキルでpush・PR作成・コンフリクト確認を行う。PRをマージしない。
- [ ] Beads/指定progressへURLと検証結果を記録し、moores-wt rmで自分のEditor/worktreeを畳む。

## 判断記録（ADR）

- ユーザー裁定: タスク指定「既存の右クリック短押し/Escによる終了を設置・破壊モードの操作案内に追加」「入力挙動は変えない」「全言語を揃える」。出所2026-10-11、.decisions/2026-10-11-設置と破壊モードにEscと右短押しの終了案内を表示する.md。
- agent前提: B/Gの即終了を先頭、新しい二段階解除案内を2行目とする。根拠: 既存PlaceBlockState/GetNextUpdate・DeleteObjectState/HandleTransition。読みやすさは実画面で検証する。
- agent前提: 新案内は2モード共通キー。根拠: 両者は同一キャンセル判定を使い、意味が同じ。選択/起点という包括表現で入力改変を避ける。
- ADR0032/0046を参照。Esc/右短押し非表示は今回2モードのみ上書きし、所有権・二段階解除の規約は保持する。

- user-simulator review: Criticalなし。既存カタログの件数10/7→11/8とEscの画面別許可範囲を更新する。ユーザー反応の採点は未観測（自主進行の委任あり）。
- Codex独立質問 generate/filter: answered 0・remaining 0。
