# 機械ブロックUI タブ化・大型化 設計

日付: 2026-07-21 / 対象: moorestech_web/webui

## 背景

機械ブロック（電気汎用工作装置等）のUIは、入出力スロット・電力率・レシピ選択行が
小型パネルへ縦積みされ、レシピが多い機械では選択行が横一列に詰め込まれて視認性が悪い。
レシピの材料も表示されず、選択判断に必要な情報が欠けている。

## 決定事項

- **対象**: レシピ選択を持つ機械ブロック（解放済みレシピが1件以上ある機械）のみ。
  チェスト・発電機・採掘機等は現状の小型パネルを維持する（YAGNI）。
- **サイズ/配置**: 研究パネルと同様に stage の `viewer-start / items-end` 2列を占有する。
  上端は持ち物パネルと揃え（研究と異なり持ち物が同時表示されるため）、
  下端はホットバー手前（stage y640 相当、高さ525px）。
- **タブ切替**: `shared/ui` の `ModeSwitch` を横向きタブバーとして流用。
  - **インベントリタブ**（デフォルト）: 入出力/モジュールスロット・進捗矢印・流体行・分間生産数。
  - **レシピ選択タブ**: 解放済みレシピを9列 `SlotGrid` で折返し列挙（左クリック選択・
    右クリック解除の現行契約を維持）＋選択中レシピの詳細（材料→出力・所要時間、
    `MachineRecipeView` 様式に準拠）。
- **電力率テキスト**: タブの外の共通フッタとして両タブで常時表示（稼働状態の常時視認）。
- **様式が先**: webui-design スキル §2.5 / §8.7 を先に更新してから実装する。

## 実装構成

- `BlockInventoryPanel`: レシピ有り機械のとき大型レイアウト用 class を付与
  （`machineRecipes` topic ＋ `buildMachineRecipeSelectionRows` で判定）。
- `MachineSection`: オーケストレータ。レシピ0件なら従来スタック、
  1件以上なら ModeSwitch ＋ タブ内容 ＋ 共通フッタ。
- `details/machine/` 新設: `MachineInventoryBody`（現行グリッド群を移設）、
  `MachineRecipeSelectionTab`（選択グリッド＋詳細）、`machineRecipeSelectionLogic`（移設）。
- 旧 `MachineRecipeSelectionRow` は削除（タブへ置換）。
- 既存 testid（`machine-section` / `machine-recipe-selection` / `machine-recipe-{guid}` 等）は維持し、
  e2e はタブクリックを追加する形で更新。

## テスト

- vitest: 設計ホワイトリストテスト・選択ロジックテストを新構造へ更新。
- Playwright e2e: タブ切替・レシピ選択/解除・大型パネル適用・非機械ブロック非適用を検証。
