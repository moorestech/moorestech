# 0054. 機械レシピ行の液体はアイテムと同じ枠（SlotFrame）に量バッジで描く

日付: 2026-09-10
状態: 採択

## Context

機械のレシピ選択行（`MachineRecipeSelectionRow`）と選択中レシピ表示（`SelectedRecipeHeader`）は、ADR 0042 で液体入出力を持つレシピを扱うようになった際、液体を `<FluidIcon>` の素置き（寸法クラス無し）で描いていた。`FluidIcon` は寸法を持たない薄いラッパで、呼び出し側が className で寸法を与える設計のため、画像が原寸で描かれ石油蒸留機の行が液体アイコンで埋め尽くされる（ユーザー報告 2026-09-10、スクリーンショット）。アイテム側は `ItemSlot`→`SlotFrame` が `--slot-size`（`recipeSlotLayout` が列幅と点数から算出）に収まっており、液体だけ枠が無い。

インベントリモードの液体は `FluidSlot`（3rem固定・暗面・amount/capacity の背面フィル）で描かれるが、レシピには容量の概念が無い（webui-design §8: 研究のレシピ表示も同理由で `FluidSlot` の充填率表現を使わない）。

## Decision

- **レシピ行と選択中レシピ表示の液体は、アイテムと同じ `SlotFrame`（`--slot-size` 追従）に液体アイコンを入れ、右下にレシピ量（加工1回あたりの必要量/生産量）のバッジを出す。充填フィルは持たない。**
  出所: ユーザー裁定 2026-09-10 原文「石油蒸留器のUI壊れてるか直したい。これはレシピ選択UI」→ 選択「アイテムと同じ枠+量バッジ」（[[2026-09-10-レシピ行の液体はアイテムと同じ枠に量バッジで描く]]）
  棄却案: ①`FluidSlot` をそのまま流用（3rem固定でアイテム枠と寸法が揃わず2列折り返しで行高が伸びる） ②`FluidIcon` に寸法だけ与え枠も量も付けない最小修正（アイテムと見た目が食い違い液体量が読めない）

- **液体スロットの面はアイテムと同じ白面（`data-filled`）。**
  出所: ユーザー裁定 2026-09-10 選択「アイテムと同じ白面」
  棄却案: インベントリモードの `FluidSlot` と同じ暗面（同じ行の白面アイテムと並ぶと液体だけ暗く見える）

- agent前提:
  1. 新しい1マス表現は `shared/ui` に置く（webui-design §8「アイテム・ブロック・液体を1マスで表すものは `shared/ui` のコンポーネントのみ」）。`FluidSlot` は容量つきタンクの表現として残し、容量の無いレシピ量表現を別部品にする（役割で分ける）
  2. 量バッジの文字は `FluidSlot` と同じ `formatAmount`（N0形式）、配置と文字寸法は `ItemSlot` の個数バッジと同じCSS変数（`--count-bottom` / `--count-font-size` / `--count-letter-spacing`）を読む。レシピ行はこれらを `.recipeMaterials` / `.recipeResult` から注入済み
  3. ホバーで液体名を出す（`ItemSlot` と同じ `HoverTooltip`）
  4. 選択中レシピ表示（`SelectedRecipeHeader`）も同じ部品を使う。ここでは `ItemSlot` 同様に量は出さない（現行のアイテム側が `count` 無しで描いているのに揃える）
  5. 既存 unit テストは `FluidIcon` をモックしているため寸法崩れを検出できない。e2e（Playwright + mock-host）に液体入出力レシピを持つ機械を足し、行内の液体スロットの矩形がアイテムスロットと同寸であることを番人にする

## Consequences

- `MachineRecipeSelectionRow` / `SelectedRecipeHeader` から `FluidIcon` の直接使用が消える。`FluidIcon` の残る呼び出し側は `FluidSlot` と `PumpSection`（どちらも寸法クラスを渡している）
- mock-host の機械レシピfixtureに `inputFluids` / `outputFluids` を持つレシピが増え、`machineRecipe.spec.ts` に寸法の番人が入る
