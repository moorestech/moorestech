# 機械レシピ行の液体スロット（FluidAmountSlot）Implementation Plan

> **For the controller session (実装を担うsubagentはこのブロックを無視してよい):** このplanの実行は subagent-driven-development スキルが担う。実行モード（規模ゲート未満の単一subagent実装モード／閾値超のタスクごと派遣）は同スキルの規模ゲートに従って決める。ステップはチェックボックス（`- [ ]`）記法で書く。

**Goal:** 機械のレシピ選択行と選択中レシピ表示で、液体をアイテムと同じ枠（`SlotFrame` 白面・`--slot-size` 追従）＋レシピ量バッジで描き、`FluidIcon` 素置きによる原寸描画の崩れ（石油蒸留機）を直す。

**Architecture:** `shared/ui` に新部品 `FluidAmountSlot`（`SlotFrame` + `FluidIcon` + 量バッジ、容量・フィル無し）を置き、`MachineRecipeSelectionRow` と `SelectedRecipeHeader` の `FluidIcon` 直置きを置き換える。`FluidSlot`（容量つきタンク）はそのまま残す。番人は unit（構造）＋ e2e（矩形の実測）。

**Tech Stack:** React 18 + Mantine 8 + CSS Modules（`moorestech_web/webui`）、vitest（unit）、Playwright + mock-host（e2e）。

## Requirements

- R1. レシピ選択行（`MachineRecipeSelectionRow`）の `inputFluids` / `outputFluids` は `FluidAmountSlot` で描く。受入: 行内の液体スロットの矩形（幅・高さ）が同じ行のアイテムスロットと一致し、液体アイコン `img` の矩形がスロット矩形に収まる（e2e 実測）。
- R2. 液体スロットは `SlotFrame` の白面（`data-filled="true"`）で、右下にレシピ量（加工1回あたりの必要量/生産量）を `formatAmount`（N0形式）で出す。受入: unit で `data-filled="true"` と `1,000` 表記のバッジを確認。
- R3. 選択中レシピ表示（`SelectedRecipeHeader`）で代表出力が液体のときも `FluidAmountSlot` で描き、量バッジは出さない（アイテム側の `ItemSlot` が `count` 無しなのに揃える）。受入: unit で `amount` 未指定の呼び出し、e2e で液体のみ出力レシピ選択後にヘッダの液体スロットが白面で見える。
- R4. `MachineRecipeSelectionRow` / `SelectedRecipeHeader` から `FluidIcon` の直接使用が消える。受入: `grep -rn "FluidIcon" src/features/blockInventory/details/machine` が0件。
- R5. ホバーで液体名（`fluidNameKey`）のツールチップを出す（`ItemSlot` と同じ `HoverTooltip`）。受入: unit でツールチップ本文に辞書名が出る。
- R6. mock-host に液体入出力レシピを持つ機械 fixture を足し、e2e `machineRecipe.spec.ts` に R1/R3 の番人を入れる。受入: `pnpm test:e2e`（machineRecipe）が通る。
- R7. `webui-design` スキル §4・§8.7 に `FluidAmountSlot` を追記する。受入: 該当節に部品名と用途が書かれている。
- やらないこと: `FluidSlot`（インベントリモードのタンク表示）の変更、`PumpSection` の `FluidIcon` 使用の変更、uGUI 側、研究画面の `UnlockFluidLabel` の置き換え。

## Global Constraints

- 作業ディレクトリ: `moorestech_web/webui`（コマンドはすべてここで実行）。ブランチ `feature/machine-recipe-fluid-amount-slot` を `master` から切る。
- master 上に既存の未コミット変更 `.moorestech-external-revisions.json` と `moorestech_server/Assets/Scripts/Core.Master/_CompileRequester.cs` があるが本作業と無関係。**stage も revert もしない**（`git add` は必ずファイル指定で行う）。
- コメントは「// 日本語 → // English」の2行セット、各1行。自明なコメントは書かない（AGENTS.md）。
- 1ファイル200行以下。`shared/ui` の新部品はサブディレクトリ（`FluidAmountSlot/index.tsx` + `style.module.css` + `index.test.ts`）に置く（前例 `BlockSlot`）。
- CSS: px直書き禁止・`--slot-size` 等のトークンを使う。`composes` で `ItemSlot/style.module.css` の `.icon` / `.count` を再利用する（前例 `RecipeBox.module.css` の `composes: recipeActionButton from "./RecipeActionButton.module.css"`）。
- アイコン上の文字は共有クラス `iconTextOutlineLight`（黒文字に白縁）を TSX で合成する（ADR 0033、`ItemSlot .count` と同形）。
- Unity 側 `.cs` は触らないため `uloop compile` は不要。Web 側は `pnpm lint` と `pnpm build`（`tsc -b`）を通す。
- e2e は `pnpm exec playwright test --config e2e/playwright.config.ts machineRecipe` で単一 spec を回す（webServer が `pnpm build` を含むため1回2分程度）。
- 各タスク末尾でコミット。コミットメッセージ末尾に session の Co-Authored-By / Claude-Session 行を付ける。

---

### Task 1: `FluidAmountSlot`（shared/ui の新部品）

**Files:**
- Create: `moorestech_web/webui/src/shared/ui/FluidAmountSlot/index.tsx`
- Create: `moorestech_web/webui/src/shared/ui/FluidAmountSlot/style.module.css`
- Create: `moorestech_web/webui/src/shared/ui/FluidAmountSlot/index.test.ts`
- Modify: `moorestech_web/webui/src/shared/ui/index.ts:7`（`FluidIcon` export の直後に追加）
- Modify: `moorestech_web/webui/src/app/iconTextOutlineDesign.test.ts:15,47`

**Interfaces:**
- Consumes: `SlotFrame`（`../SlotFrame`、props `testId?`, `filled?`）、`FluidIcon`（`../FluidIcon`、props `fluidGuid`, `className?`）、`HoverTooltip`（`../HoverTooltip`）、`formatAmount`（`../FluidSlot/fluidLogic`）、`fluidNameKey` / `useI18n`（`@/shared/i18n`）
- Produces: `export default function FluidAmountSlot(props: { fluidGuid: string; amount?: number; testId?: string }): JSX.Element` を `@/shared/ui` から `FluidAmountSlot` 名で export

- [ ] **Step 1: 失敗するテストを書く**

`moorestech_web/webui/src/shared/ui/FluidAmountSlot/index.test.ts`:

```ts
import { createElement } from "react";
import { renderToStaticMarkup } from "react-dom/server";
import { MantineProvider } from "@mantine/core";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { fluidNameKey } from "@/shared/i18n/contentKeys";
import { setDictionaries } from "@/shared/i18n/i18nStore";
import FluidAmountSlot from "./index";
import styles from "./style.module.css";

const FLUID_GUID = "60000000-0000-4000-8000-000000000001";

// ツールチップ本文は開いてからでないとDOMへ出ないため、静的描画で本文を読めるスタブへ差し替える
// The tooltip body only reaches the DOM once opened, so a stub renders it inline for static markup
vi.mock("../HoverTooltip", () => ({
  default: ({ label, children }: { label?: unknown; children?: unknown }) =>
    createElement("mock-hover-tooltip", null, label as never, children as never),
}));

function renderSlot(amount?: number) {
  return renderToStaticMarkup(
    createElement(MantineProvider, null, createElement(FluidAmountSlot, { fluidGuid: FLUID_GUID, amount, testId: "fluid-amount" })),
  );
}

describe("FluidAmountSlot", () => {
  beforeEach(() => {
    const key = fluidNameKey(FLUID_GUID);
    setDictionaries("japanese", { [key]: "水" }, { [key]: "Water" }, { [key]: "Water" });
  });

  it("白面のSlotFrameに寸法クラス付きの液体アイコンを描く", () => {
    const markup = renderSlot(1000);

    expect(markup).toContain('data-filled="true"');
    expect(markup).toContain('data-testid="fluid-amount"');
    expect(markup).toContain(`/api/fluid-icons/${FLUID_GUID}.png`);
    expect(markup).toContain(`class="${styles.icon}"`);
  });

  it("量はN0形式のバッジで右下に出し、アイコン文字の白縁クラスを合成する", () => {
    const markup = renderSlot(1000);

    expect(markup).toContain(">1,000<");
    expect(markup).toContain(`iconTextOutlineLight ${styles.amount}`);
  });

  it("amount未指定ならバッジを出さない", () => {
    const markup = renderSlot(undefined);

    expect(markup.match(/<span/g)).toBeNull();
  });

  it("ホバーツールチップに辞書の液体名を出す", () => {
    const markup = renderSlot(1000);

    expect(markup).toContain("<mock-hover-tooltip>水");
  });
});
```

- [ ] **Step 2: テストを実行して失敗を確認する**

Run: `cd moorestech_web/webui && pnpm vitest run src/shared/ui/FluidAmountSlot`
Expected: FAIL（`Failed to resolve import "./index"`）

- [ ] **Step 3: CSS を書く**

`moorestech_web/webui/src/shared/ui/FluidAmountSlot/style.module.css`:

```css
/* 容量の無いレシピ量表示。アイコン寸法と量バッジはItemSlotと同じ規則を借りて行内のアイテムと揃える */
/* Amount-only fluid presentation without capacity; the icon size and badge reuse ItemSlot's rules so it lines up with items in a row */
.icon {
  composes: icon from "../ItemSlot/style.module.css";
}

.amount {
  composes: count from "../ItemSlot/style.module.css";
}
```

- [ ] **Step 4: コンポーネントを書く**

`moorestech_web/webui/src/shared/ui/FluidAmountSlot/index.tsx`:

```tsx
import HoverTooltip from "../HoverTooltip";
import FluidIcon from "../FluidIcon";
import SlotFrame from "../SlotFrame";
import { formatAmount } from "../FluidSlot/fluidLogic";
import { fluidNameKey, useI18n } from "@/shared/i18n";
import styles from "./style.module.css";

type Props = {
  fluidGuid: string;
  // レシピ量（加工1回あたりの必要量/生産量）。未指定ならバッジを出さない
  // Recipe amount (per-process input/output); no badge when omitted
  amount?: number;
  testId?: string;
};

// レシピ行・選択中レシピ表示向けの液体1マス。容量の概念が無いため充填フィルは持たず、面はアイテムと同じ白面（ADR 0054）
// One fluid cell for recipe rows and the selected-recipe header; no capacity so no fill, and the face is the same white as items (ADR 0054)
export default function FluidAmountSlot({ fluidGuid, amount, testId }: Props) {
  const { t } = useI18n();
  const name = t(fluidNameKey(fluidGuid));
  return (
    <HoverTooltip label={name} disabled={!name}>
      <SlotFrame testId={testId} filled>
        <FluidIcon fluidGuid={fluidGuid} className={styles.icon} />
        {amount !== undefined && amount > 0 ? <span className={`iconTextOutlineLight ${styles.amount}`}>{formatAmount(amount)}</span> : null}
      </SlotFrame>
    </HoverTooltip>
  );
}
```

- [ ] **Step 5: `shared/ui/index.ts` に export を足す**

`moorestech_web/webui/src/shared/ui/index.ts` の `export { default as FluidIcon } from "./FluidIcon";` の直後に追加:

```ts
export { default as FluidAmountSlot } from "./FluidAmountSlot";
```

- [ ] **Step 6: テストを実行して通ることを確認する**

Run: `cd moorestech_web/webui && pnpm vitest run src/shared/ui/FluidAmountSlot`
Expected: PASS（4 tests）

- [ ] **Step 7: 縁取り様式の設計テストへ新しい適用先を登録する**

`moorestech_web/webui/src/app/iconTextOutlineDesign.test.ts` の `const fluidSlotCss = ...` の直後に追加:

```ts
const fluidAmountSlotTsx = read("../shared/ui/FluidAmountSlot/index.tsx");
const fluidAmountSlotCss = read("../shared/ui/FluidAmountSlot/style.module.css");
```

`it("黒文字が白縁の共有クラスを持ち、擬似縁を残さない", ...)` の本文末尾に追加:

```ts
    // ADR 0054: レシピ量バッジも黒文字なので白縁を1回だけ合成し、CSSはItemSlotの.countを借りる
    // ADR 0054: the recipe amount badge is black text too, composing the light outline once and borrowing ItemSlot's .count
    expect(fluidAmountSlotTsx.match(/iconTextOutlineLight/g)).toHaveLength(1);
    expect(fluidAmountSlotCss).toContain('composes: count from "../ItemSlot/style.module.css"');
    expect(fluidAmountSlotCss).not.toContain("text-shadow");
```

Run: `cd moorestech_web/webui && pnpm vitest run src/app/iconTextOutlineDesign`
Expected: PASS

- [ ] **Step 8: lint を通す**

Run: `cd moorestech_web/webui && pnpm lint`
Expected: エラー0

- [ ] **Step 9: コミットする**

```bash
git add moorestech_web/webui/src/shared/ui/FluidAmountSlot moorestech_web/webui/src/shared/ui/index.ts moorestech_web/webui/src/app/iconTextOutlineDesign.test.ts
git commit -m "feat(webui): 容量の無い液体量スロット FluidAmountSlot を shared/ui に追加 (ADR 0054)"
```

---

### Task 2: レシピ選択行の液体を `FluidAmountSlot` へ置換

**Files:**
- Modify: `moorestech_web/webui/src/features/blockInventory/details/machine/recipeSelection/MachineRecipeSelectionRow.tsx:4,33-41`
- Test: `moorestech_web/webui/src/features/blockInventory/details/machine/recipeSelection/MachineRecipeSelectionRow.test.ts`

**Interfaces:**
- Consumes: Task 1 の `FluidAmountSlot`（`@/shared/ui`）
- Produces: 行内液体スロットの testId 規約 `machine-recipe-<recipeGuid>-input-fluid-<i>` / `machine-recipe-<recipeGuid>-output-fluid-<i>`（Task 4 の e2e が参照）

- [ ] **Step 1: 失敗するテストを書く**

`MachineRecipeSelectionRow.test.ts` の `vi.mock("@/shared/ui", ...)` を次に差し替える（`FluidIcon` のモックを外し、`FluidAmountSlot` を足す。行が `FluidIcon` を import し続けると undefined 要素で描画が落ちるので、それ自体が R4 の番人になる）:

```ts
vi.mock("@/shared/ui", () => ({
  ItemSlot: (props: object) => createElement("mock-item-slot", props),
  FluidAmountSlot: (props: object) => createElement("mock-fluid-amount-slot", props),
  ProgressArrowGlyph: (props: object) => createElement("mock-arrow", props),
}));
```

`describe` 末尾に追加:

```ts
  // ADR 0054: 液体は寸法無しのFluidIconでなく、アイテムと同じ枠のFluidAmountSlotで量つきに描く
  // ADR 0054: fluids render through FluidAmountSlot (same frame as items, with amount), never a bare unsized FluidIcon
  it("入出力の液体はFluidAmountSlotへ量とtestIdを渡して描く", () => {
    const fluidRecipe: MachineRecipe = {
      ...recipe,
      inputFluids: [{ fluidGuid: "87000000-0000-4000-8000-000000000001", amount: 10 }],
      outputFluids: [{ fluidGuid: "87000000-0000-4000-8000-000000000002", amount: 1000 }],
    };
    const row = { recipe: fluidRecipe, subject: { kind: "item" as const, itemId: 9, count: 1 }, selected: false };
    const tree = create(createElement(MachineRecipeSelectionRow, { row, onSelect: vi.fn() }));

    const slots = tree.root.findAllByType("mock-fluid-amount-slot" as never);
    expect(slots.map((slot) => [slot.props.fluidGuid, slot.props.amount, slot.props.testId])).toEqual([
      ["87000000-0000-4000-8000-000000000001", 10, `machine-recipe-${recipe.recipeGuid}-input-fluid-0`],
      ["87000000-0000-4000-8000-000000000002", 1000, `machine-recipe-${recipe.recipeGuid}-output-fluid-0`],
    ]);
  });
```

- [ ] **Step 2: テストを実行して失敗を確認する**

Run: `cd moorestech_web/webui && pnpm vitest run src/features/blockInventory/details/machine/recipeSelection`
Expected: FAIL（既存2件も含め `FluidIcon` が undefined で "Element type is invalid" になる）

- [ ] **Step 3: 行を書き換える**

`MachineRecipeSelectionRow.tsx` の import 行:

```tsx
import { FluidAmountSlot, ItemSlot } from "@/shared/ui";
```

`materials` / `result` の液体 map を次に差し替える:

```tsx
        materials={[
          ...recipe.inputItems.map((item, i) => <ItemSlot key={`item-${i}`} itemId={item.itemId} count={item.count} />),
          ...recipe.inputFluids.map((fluid, i) => (
            <FluidAmountSlot key={`fluid-${i}`} fluidGuid={fluid.fluidGuid} amount={fluid.amount} testId={`machine-recipe-${recipe.recipeGuid}-input-fluid-${i}`} />
          )),
        ]}
        actionMode="none"
        result={[
          ...recipe.outputItems.map((item, i) => <ItemSlot key={`item-${i}`} itemId={item.itemId} count={item.count} />),
          ...recipe.outputFluids.map((fluid, i) => (
            <FluidAmountSlot key={`fluid-${i}`} fluidGuid={fluid.fluidGuid} amount={fluid.amount} testId={`machine-recipe-${recipe.recipeGuid}-output-fluid-${i}`} />
          )),
        ]}
```

- [ ] **Step 4: テストを実行して通ることを確認する**

Run: `cd moorestech_web/webui && pnpm vitest run src/features/blockInventory/details/machine/recipeSelection`
Expected: PASS（3 tests）

- [ ] **Step 5: コミットする**

```bash
git add moorestech_web/webui/src/features/blockInventory/details/machine/recipeSelection/MachineRecipeSelectionRow.tsx moorestech_web/webui/src/features/blockInventory/details/machine/recipeSelection/MachineRecipeSelectionRow.test.ts
git commit -m "fix(webui): 機械レシピ選択行の液体をFluidAmountSlotで描き原寸描画の崩れを直す (ADR 0054)"
```

---

### Task 3: 選択中レシピ表示の液体を `FluidAmountSlot` へ置換

**Files:**
- Modify: `moorestech_web/webui/src/features/blockInventory/details/machine/SelectedRecipeHeader.tsx:5,22`
- Create: `moorestech_web/webui/src/features/blockInventory/details/machine/SelectedRecipeHeader.test.ts`

**Interfaces:**
- Consumes: Task 1 の `FluidAmountSlot`
- Produces: ヘッダ液体スロットの testId `machine-selected-recipe-fluid`（Task 4 の e2e が参照）

- [ ] **Step 1: 失敗するテストを書く**

`SelectedRecipeHeader.test.ts`:

```ts
import { createElement } from "react";
import { create } from "react-test-renderer";
import { describe, expect, it, vi } from "vitest";
import type { MachineRecipe } from "@/bridge";

vi.mock("@/shared/i18n", async (importOriginal) => ({
  ...(await importOriginal<typeof import("@/shared/i18n")>()),
  useI18n: () => ({ t: (key: string) => key }),
  useItemNameResolver: () => (itemId: number) => `item-${itemId}`,
}));
vi.mock("@mantine/core", () => ({
  Group: ({ children, ...props }: { children: unknown }) => createElement("mock-group", props, children as never),
  Text: ({ children, ...props }: { children: unknown }) => createElement("mock-text", props, children as never),
}));
vi.mock("@/shared/ui", () => ({
  HoverTooltip: ({ children }: { children: unknown }) => createElement("mock-hover-tooltip", null, children as never),
  ItemSlot: (props: object) => createElement("mock-item-slot", props),
  FluidAmountSlot: (props: object) => createElement("mock-fluid-amount-slot", props),
}));

import SelectedRecipeHeader from "./SelectedRecipeHeader";

const recipe: MachineRecipe = {
  recipeGuid: "84000000-0000-4000-8000-000000000001",
  blockGuid: "85000000-0000-4000-8000-000000000001",
  blockId: 10, time: 7,
  inputItems: [], outputItems: [],
  inputFluids: [], outputFluids: [{ fluidGuid: "87000000-0000-4000-8000-000000000001", amount: 100 }],
};

describe("SelectedRecipeHeader", () => {
  // ADR 0054: 代表が液体でもアイテムと同じ枠で描き、ヘッダでは量バッジを出さない（ItemSlot側もcount無し）
  // ADR 0054: a fluid representative uses the same frame as items, and the header shows no amount badge (the ItemSlot side has no count either)
  it("代表が液体のときはFluidAmountSlotを量無しで描く", () => {
    const subject = { kind: "fluid" as const, fluidGuid: "87000000-0000-4000-8000-000000000001", amount: 100 };
    const tree = create(createElement(SelectedRecipeHeader, { recipe, subject, onChangeRecipe: vi.fn() }));

    const slot = tree.root.findByType("mock-fluid-amount-slot" as never);
    expect(slot.props.fluidGuid).toBe("87000000-0000-4000-8000-000000000001");
    expect(slot.props.amount).toBeUndefined();
    expect(slot.props.testId).toBe("machine-selected-recipe-fluid");
    expect(tree.root.findAllByType("mock-item-slot" as never)).toHaveLength(0);
  });

  it("代表がアイテムのときはItemSlotを描く", () => {
    const subject = { kind: "item" as const, itemId: 9, count: 1 };
    const tree = create(createElement(SelectedRecipeHeader, { recipe, subject, onChangeRecipe: vi.fn() }));

    expect(tree.root.findByType("mock-item-slot" as never).props.itemId).toBe(9);
    expect(tree.root.findAllByType("mock-fluid-amount-slot" as never)).toHaveLength(0);
  });
});
```

- [ ] **Step 2: テストを実行して失敗を確認する**

Run: `cd moorestech_web/webui && pnpm vitest run src/features/blockInventory/details/machine/SelectedRecipeHeader`
Expected: FAIL（`FluidIcon` が undefined で "Element type is invalid"）

- [ ] **Step 3: ヘッダを書き換える**

`SelectedRecipeHeader.tsx` の import 行:

```tsx
import { FluidAmountSlot, HoverTooltip, ItemSlot } from "@/shared/ui";
```

`{subject.kind === "item" ? ... }` の行を次に差し替える:

```tsx
        {subject.kind === "item" ? <ItemSlot itemId={subject.itemId} /> : <FluidAmountSlot fluidGuid={subject.fluidGuid} testId="machine-selected-recipe-fluid" />}
```

- [ ] **Step 4: テストを実行して通ることを確認する**

Run: `cd moorestech_web/webui && pnpm vitest run src/features/blockInventory/details/machine`
Expected: PASS（新規2件を含め `details/machine` 配下が全通過）

- [ ] **Step 5: R4 の確認と全 unit・lint・build**

Run: `cd moorestech_web/webui && grep -rn "FluidIcon" src/features/blockInventory/details/machine; pnpm vitest run; pnpm lint; pnpm build`
Expected: grep 0件、vitest 全通過、lint エラー0、build 成功

- [ ] **Step 6: コミットする**

```bash
git add moorestech_web/webui/src/features/blockInventory/details/machine/SelectedRecipeHeader.tsx moorestech_web/webui/src/features/blockInventory/details/machine/SelectedRecipeHeader.test.ts
git commit -m "fix(webui): 選択中レシピ表示の液体代表をFluidAmountSlotで描く (ADR 0054)"
```

---

### Task 4: e2e の番人（mock-host fixture ＋ 矩形実測）

**Files:**
- Modify: `moorestech_web/webui/e2e/mock-host/fixtures/recipeFixtures.ts:70-77`（`cccccccc` の後に液体レシピを追加）
- Modify: `moorestech_web/webui/e2e/mock-host/blockDetailFixtures.ts:18`（`fluidSlots` を2タンクにする）
- Modify: `moorestech_web/webui/e2e/tests/block/machineRecipe.spec.ts:40`（行数 3→4）＋ 末尾にテスト2件追加

**Interfaces:**
- Consumes: Task 2 の testId `machine-recipe-<guid>-input-fluid-0`、Task 3 の testId `machine-selected-recipe-fluid`
- Produces: 新レシピ guid `ffffffff-ffff-4fff-8fff-ffffffffffff`（電気機械・液体のみ入出力）

- [ ] **Step 1: mock-host に液体のみ入出力のレシピを足す**

`recipeFixtures.ts` の `cccccccc` エントリの直後（`dddddddd` の前）に追加:

```ts
    {
      // 液体のみ入出力のレシピ（石油蒸留機型）。選択行の液体スロット寸法とヘッダの液体代表のe2e用
      // A fluid-only input/output recipe (distiller-like) for the e2e on selection-row fluid slot size and the fluid header representative
      recipeGuid: "ffffffff-ffff-4fff-8fff-ffffffffffff",
      blockGuid: ELECTRIC_MACHINE_BLOCK_GUID,
      blockId: 3, time: 12,
      inputItems: [], outputItems: [],
      inputFluids: [{ fluidGuid: WATER_FLUID_GUID, amount: 10 }], outputFluids: [{ fluidGuid: WATER_FLUID_GUID, amount: 1000 }],
    },
```

- [ ] **Step 2: 電気機械 fixture の出力タンクを用意する**

`blockDetailFixtures.ts` の `blockMachine.fluidSlots` を次に差し替える（`slotLayout.inputTank` は 1 のまま。出力液体は tank index 1 に束縛されるため2本目が要る）:

```ts
  // 入力タンク1本＋出力タンク1本。液体のみ入出力レシピ(ffffffff)を選ぶと出力液体がtank 1へ束縛される
  // One input tank plus one output tank; selecting the fluid-only recipe (ffffffff) binds its output fluid to tank 1
  fluidSlots: [{ fluidId: 0, amount: 0, capacity: 100.0, fluidGuid: "" }, { fluidId: 0, amount: 0, capacity: 100.0, fluidGuid: "" }],
```

- [ ] **Step 3: 既存の行数アサートを更新する**

`machineRecipe.spec.ts` の `toHaveCount(3)`（`[data-testid^="machine-recipe-"][data-testid$="-name"]` の行）を `toHaveCount(4)` にする。

- [ ] **Step 4: 失敗する e2e を書く**

`machineRecipe.spec.ts` の先頭定数の下に追加:

```ts
const fluidRecipeTestId = "machine-recipe-ffffffff-ffff-4fff-8fff-ffffffffffff";

// mock-hostは液体アイコンを404にするため、原寸描画の崩れ（ADR 0054の起点）を再現するには大きな画像を実際に読ませる必要がある
// The mock host 404s fluid icons, so reproducing the natural-size overflow (ADR 0054's origin) needs a large image to actually load
const largeFluidIconSvg = '<svg xmlns="http://www.w3.org/2000/svg" width="512" height="512"><rect width="512" height="512" fill="#2A6FE0"/></svg>';

async function serveLargeFluidIcons(page: Page) {
  await page.route("**/api/fluid-icons/*.png", (route) => route.fulfill({ contentType: "image/svg+xml", body: largeFluidIconSvg }));
}
```

import 行を `import { test, expect, type Page } from "@playwright/test";` にする。

ファイル末尾に追加:

```ts
test("レシピ選択行の液体はアイテムスロットと同寸の枠に収まり、量バッジを出す", async ({ page }) => {
  await serveLargeFluidIcons(page);
  await setBlock(page, "machine");
  await page.goto("/");
  await page.getByTestId("machine-selected-recipe").click();
  await expect(page.getByTestId("machine-recipe-selection")).toBeVisible();

  const row = page.getByTestId(`${selectedRecipeTestId}-row`);
  // 素材列のアイテムスロットはtestIdを持たず、液体スロットだけがtestIdを持つ
  // Material item slots carry no testId; only the fluid slot does
  const itemSlot = row.locator('[data-filled="true"]:not([data-testid])').first();
  const fluidSlot = page.getByTestId(`${selectedRecipeTestId}-input-fluid-0`);
  await expect(fluidSlot).toHaveAttribute("data-filled", "true");
  await expect(fluidSlot).toContainText("10");

  const itemBox = (await itemSlot.boundingBox())!;
  const fluidBox = (await fluidSlot.boundingBox())!;
  expect(fluidBox.width).toBeCloseTo(itemBox.width, 0);
  expect(fluidBox.height).toBeCloseTo(itemBox.height, 0);

  // 512pxの原画像が枠内に収まっている（寸法クラス欠落なら枠を突き抜ける）
  // The 512px source image stays inside the frame (a missing size class would let it overflow)
  const iconBox = (await fluidSlot.locator("img").boundingBox())!;
  expect(iconBox.width).toBeLessThanOrEqual(fluidBox.width + 0.5);
  expect(iconBox.height).toBeLessThanOrEqual(fluidBox.height + 0.5);
});

test("液体のみ出力のレシピを選ぶと選択中レシピ表示が液体スロットになる", async ({ page }) => {
  await serveLargeFluidIcons(page);
  await setBlock(page, "machine");
  await page.goto("/");
  await page.getByTestId("machine-selected-recipe").click();
  await page.getByTestId(fluidRecipeTestId).click();

  await expect(page.getByTestId("machine-inventory-body")).toBeVisible();
  const headerFluid = page.getByTestId("machine-selected-recipe-fluid");
  await expect(headerFluid).toHaveAttribute("data-filled", "true");
  await expect(headerFluid.locator("span")).toHaveCount(0);
  const headerBox = (await headerFluid.boundingBox())!;
  const iconBox = (await headerFluid.locator("img").boundingBox())!;
  expect(iconBox.width).toBeLessThanOrEqual(headerBox.width + 0.5);
  // 入力タンク1＋出力タンク1がゴーストで描かれる
  // One input tank plus one output tank render as ghosts
  await expect(page.getByTestId("machine-fluid-slots").locator('[data-ghost="true"]')).toHaveCount(2);
});
```

- [ ] **Step 5: e2e を実行して結果を確認する**

Run: `cd moorestech_web/webui && pnpm exec playwright test --config e2e/playwright.config.ts machineRecipe`
Expected: PASS（Task 1〜3 適用済みなので新規2件を含め全通過）。番人が効いていることの確認として、一時的に `FluidAmountSlot/style.module.css` の `.icon` ブロックをコメントアウトして再実行し、「レシピ選択行の液体は…」が `iconBox.width` で FAIL することを確かめてから元に戻す（この一時変更はコミットしない）。

- [ ] **Step 6: e2e の型検査を通す**

Run: `cd moorestech_web/webui && tsc -p e2e/tsconfig.json --noEmit`
Expected: エラー0

- [ ] **Step 7: コミットする**

```bash
git add moorestech_web/webui/e2e/mock-host/fixtures/recipeFixtures.ts moorestech_web/webui/e2e/mock-host/blockDetailFixtures.ts moorestech_web/webui/e2e/tests/block/machineRecipe.spec.ts
git commit -m "test(webui e2e): 機械レシピ行の液体スロット寸法と液体代表ヘッダの番人を追加 (ADR 0054)"
```

---

### Task 5: 設計文書の追従（webui-design §4 / §8.7）

**Files:**
- Modify: `.agents/skills/webui-design/SKILL.md:158`（§4 部品一覧）、`:162`（`FluidSlot` 3層の項の直後）、`:175`（縁の適用先一覧）、`:283`（§8.7 選択中レシピ表示）、`:293`（§8.7 行リスト）

- [ ] **Step 1: §4 の部品一覧に追記する**

`- `ItemSlot` / `BlockSlot` / `FluidSlot` / `FluidSlotRow` / 素枠は `SlotFrame`。` を次に差し替える:

```md
  - `ItemSlot` / `BlockSlot` / `FluidSlot` / `FluidAmountSlot` / `FluidSlotRow` / 素枠は `SlotFrame`。
```

`FluidSlot` 3層の項（`- **`FluidSlot` は「背面に…` で始まる行）の直後に追加:

```md
  - **`FluidAmountSlot` は容量の無い液体量の1マス**（レシピ行・選択中レシピ表示）。`SlotFrame` の白面（`data-filled`）に液体アイコン＋右下にレシピ量バッジ（`formatAmount`）で、背面フィルは持たない。タンク（amount/capacity）を表すときは `FluidSlot`、レシピ量を表すときは `FluidAmountSlot` と役割で使い分ける（ADR 0054、ユーザー裁定 2026-09-10）。
```

- [ ] **Step 2: 縁クラスの適用先一覧を更新する**

`  `FluidSlot .amount` / `HotbarPanel .num` の4箇所。` を次に差し替える:

```md
  `FluidSlot .amount` / `FluidAmountSlot .amount` / `HotbarPanel .num` の5箇所。
```

- [ ] **Step 3: §8.7 を更新する**

選択中レシピ表示の行にある `（出力 `ItemSlot`（個数バッジ無し）＋レシピ名（出力アイテム名）＋秒数、testId `machine-selected-recipe`）` を次に差し替える:

```md
（代表出力がアイテムなら `ItemSlot`、液体なら `FluidAmountSlot`。どちらもバッジ無し。＋レシピ名（代表出力名）＋秒数、testId `machine-selected-recipe`）
```

行リストの項にある `行全体（`data-testid="machine-recipe-<guid>"`）が左クリック対象で、行内の `ItemSlot` は操作を持たない。` を次に差し替える:

```md
行全体（`data-testid="machine-recipe-<guid>"`）が左クリック対象で、行内の `ItemSlot` / `FluidAmountSlot` は操作を持たない。液体は `FluidAmountSlot` でアイテムと同寸の枠に量バッジつきで描く（testId `machine-recipe-<guid>-input-fluid-<i>` / `-output-fluid-<i>`、ADR 0054）。
```

- [ ] **Step 4: 差分を目視確認してコミットする**

Run: `git diff .agents/skills/webui-design/SKILL.md`
Expected: 上記5箇所のみ変更

```bash
git add .agents/skills/webui-design/SKILL.md
git commit -m "docs(webui-design): FluidAmountSlot を §4/§8.7 に追記 (ADR 0054)"
```

---

### Task 6: 全ブランチレビュー（省略不可）

- [ ] **Step 1: moores-code-review スキルで全ブランチレビューを実行する**

必ず最後にコードレビュースキル（`moores-code-review`）で全ブランチレビューを実行すること（自動実行・ゴール文言による省略不可）。レビュー記録は `../moorestech_logs/harness/` へ書き、feature ブランチにはコミットしない。

- [ ] **Step 2: 指摘を反映する**

反映がソース（`FluidAmountSlot` の描画構造・CSS・行/ヘッダの分岐）に触れた場合は、Task 4 Step 5 の e2e を反映後のビルドで再実行してから完了とする（unit 通過は代替にならない）。

- [ ] **Step 3: 仕上げ**

Run: `cd moorestech_web/webui && pnpm vitest run && pnpm lint && pnpm build && pnpm exec playwright test --config e2e/playwright.config.ts machineRecipe`
Expected: すべて通過

```bash
bd close moorestech-gx6 --reason="FluidAmountSlot で機械レシピ行・ヘッダの液体を描き、e2e 番人と設計文書を追加"
```

その後 `pr-create` スキルで PR を作成する（本文末尾に session の attribution を付ける）。

---

## 配置と前例（spec-architecture-review）

| # | 項目 | 配置先 | 機構 | 前例 | 判定 |
|---|---|---|---|---|---|
| 1 | `FluidAmountSlot`（新規1マス部品） | `src/shared/ui/FluidAmountSlot/` | `SlotFrame` + `FluidIcon` + `HoverTooltip` | `src/shared/ui/BlockSlot/index.tsx`（SlotFrame＋アイコン＋Tooltip）、`ItemSlot`（白面＋個数バッジ） | ok（webui-design §4「1マス表現は shared/ui のみ」） |
| 2 | 量バッジのCSS | `FluidAmountSlot/style.module.css` の `composes` | CSS Modules composes | `RecipeBox.module.css` の `composes: recipeActionButton from ...` | ok |
| 3 | 量の整形 | `../FluidSlot/fluidLogic.formatAmount` を import | 既存純関数の再利用 | `FluidSlot/index.tsx` | ok |
| 4 | 行・ヘッダの置換 | `features/blockInventory/details/machine/` | feature 側は shared 部品を呼ぶだけ | `MachineRecipeSelectionRow` の `ItemSlot` 使用 | ok |
| 5 | e2e fixture | `e2e/mock-host/fixtures/recipeFixtures.ts` / `blockDetailFixtures.ts` | 既存 fixture 配列への追加 | `bbbbbbbb` の水インプット追加コメント | ok |
| 6 | e2e で液体アイコンを実際に読ませる | spec 内 `page.route` | Playwright route.fulfill | 新規パターン（mock-host は非DEMOで404固定）。mock-host 側を変えず spec 内に閉じる | 注目点（下記） |

- データフロー: 変更はすべて表示層（wire の `MachineRecipe.inputFluids/outputFluids` を読むだけ）。書き手・交差点の追加なし。
- 機能パリティ: レシピ選択行のクリック（行全体）・ヘッダクリック（選択モードへ戻る）・ゴーストスロット・液体タンク表示は不変。液体スロットは `SlotFrame` に操作ハンドラを渡さないため行クリックが引き続き効く。
- 注目点（新規パターン）: #6 の `page.route` による画像差し替え。mock-host の `respondGameIcon` を変えると全 spec の `#id` フォールバック契約に波及するため、当該 spec 内に閉じた。

## 判断記録（ADR）

- 設計ADR: `docs/adr/0054-recipe-row-fluid-slot-matches-item-slot.md`（ユーザー裁定2件: 枠＋量バッジ／白面。`.decisions/2026-09-10-レシピ行の液体はアイテムと同じ枠に量バッジで描く.md`）
- 部品名 `FluidAmountSlot`（容量の無い液体量の1マス）: agent前提（`FluidSlot`＝タンクと役割で分ける。ADR 0054 agent前提1）
- 量バッジ・アイコンCSSを `ItemSlot/style.module.css` から `composes` で借りる: agent前提（ADR 0054 agent前提2、前例 `RecipeBox.module.css`）
- ヘッダでは量を出さない: agent前提（ADR 0054 agent前提4）
- e2e の液体アイコンを spec 内 `page.route` で 512px SVG に差し替えて原寸描画の崩れを再現可能にする: agent前提（mock-host の 404 契約を変えない。ADR 0054 agent前提5 の具体化）
- 液体のみ入出力レシピ fixture を電気機械へ足し、既存の行数アサートを 3→4 に更新する: agent前提（ギア機械には選択済み fixture が無くヘッダ検証ができないため）
- `blockMachine.fluidSlots` を2本にする: agent前提（出力液体が `inputTank + j = 1` へ束縛されるため。`buildBoundFluidBand` は `fluidSlots` の index を列挙するので2本目が無いと出力ゴーストが描かれない）
