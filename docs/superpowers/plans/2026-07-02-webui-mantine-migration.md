# Web UI Mantine 移行計画 (Tailwind CSS 全面廃止)

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** `moorestech_web/webui` の全コンポーネントから Tailwind CSS を撤去し、Mantine v8 + CSS Modules に移行する。

**Architecture:** 汎用 UI（ボタン・モーダル・通知・進捗・タブ・レイアウト）は Mantine コンポーネントへ、ゲーム固有 UI（48px スロット・固定オーバーレイ・grid-template-areas レイアウト）は CSS Modules へ振り分ける。Tailwind と Mantine を一時共存させ、コンポーネント単位で段階移行し、各タスクで unit + e2e + build を green に保つ。最終タスクで Tailwind を完全撤去する。

**Tech Stack:** React 18.3 / Vite 5 / TypeScript 5.7 / Mantine v8 (@mantine/core, @mantine/hooks) / postcss-preset-mantine / CSS Modules / Vitest / Playwright

## Global Constraints

- 作業ディレクトリは `moorestech_web/webui`（コマンドはすべてここで実行）。パッケージマネージャは **pnpm**。
- **CEF オーバーレイ透過を維持**: `body { background-color: rgba(17, 17, 17, 0.6); }` が Mantine のグローバルスタイルより後に適用されること（`main.tsx` で `@mantine/core/styles.css` → `@/app/index.css` の import 順を厳守）。
- 各タスク終了時に `pnpm test`（unit）・`pnpm test:e2e`（Playwright）・`pnpm build`（tsc+vite）がすべて成功していること。
- 各タスク終了時に必ず `git commit` する（AGENTS.md: 作業消失防止）。
- コメントは「// 日本語 → // English」の2行セット。各行1行に収める。自明なコメントは書かない。
- 1ファイル200行以下・1ディレクトリ10ファイル以下。partial 禁止（C# 規約だが web でもファイル分割で対応）。
- e2e のセレクタから Tailwind クラス依存（`.grid.grid-cols-9`、`/border-yellow-400/`、`.fixed.z-40`、`.fixed.bottom-4.right-4`、`.grid > div`）を排除し、`data-testid` / `data-selected` に置き換える。
- z-index 設計: ProgressBar=20, BlockInventoryPanel=30, GrabOverlay=40, Mantine Modal=200(デフォルト), ToastHost=300。

## 対象ファイル一覧（現状の Tailwind 使用箇所）

| ファイル | 移行先 |
|---|---|
| `src/app/App.tsx` | Group + Title + App.module.css 拡張 |
| `src/app/App.module.css` | padding / min-height 追加 |
| `src/app/index.css` | @tailwind 3行削除 |
| `src/app/DebugActionButton.tsx` | Mantine Button |
| `src/shared/ui/ItemSlot/` | CSS Module + Mantine Tooltip |
| `src/shared/ui/FluidSlot/` | CSS Module + Mantine Tooltip |
| `src/shared/ui/ItemIcon.tsx` | CSS Module（フォールバック表示） |
| `src/shared/ui/ProgressArrow/` | CSS Module |
| `src/shared/ui/SlotGrid/`（新規） | 9列スロットグリッド共通化 |
| `src/features/inventory/InventoryPanel/` | Stack/Group/Button/Title + SlotGrid |
| `src/features/recipe/*` | Tabs/Stack/Group/Button/ActionIcon/ScrollArea |
| `src/features/blockInventory/*` | Paper + SlotGrid + Group |
| `src/features/modal/ModalHost.tsx` + `modalLogic.ts` | Mantine Modal compound + buttonColor |
| `src/features/toast/ToastHost.tsx` | Mantine Notification |
| `src/features/progress/ProgressBar.tsx` + `progressLogic.ts` | Mantine Progress compound + percentValue |
| `tailwind.config.{ts,js,d.ts}` / `postcss.config.js` / `package.json` | Tailwind 撤去 |
| `e2e/tests/{hotbar,inventory,blockInventory,fluidSlot,recipe}.spec.ts` | testid/data 属性セレクタ化 |

---

### Task 1: Mantine 基盤導入（Tailwind と共存）

**Files:**
- Modify: `package.json`（pnpm コマンド経由）
- Modify: `postcss.config.js`
- Modify: `src/main.tsx`

**Interfaces:**
- Consumes: なし
- Produces: 全後続タスクが前提とする `MantineProvider`（`defaultColorScheme="dark"`）と `@mantine/core` のコンポーネント群

- [ ] **Step 1: パッケージ追加**

```bash
cd /Users/katsumi/moorestech-worktrees/tree2/moorestech_web/webui
pnpm add @mantine/core@^8 @mantine/hooks@^8
pnpm add -D postcss-preset-mantine postcss-simple-vars
```

Expected: `package.json` の dependencies に `@mantine/core` / `@mantine/hooks` が入る。

- [ ] **Step 2: postcss.config.js を Tailwind + Mantine 共存構成に更新**

```js
export default {
  plugins: {
    "postcss-preset-mantine": {},
    "postcss-simple-vars": {
      variables: {
        "mantine-breakpoint-xs": "36em",
        "mantine-breakpoint-sm": "48em",
        "mantine-breakpoint-md": "62em",
        "mantine-breakpoint-lg": "75em",
        "mantine-breakpoint-xl": "88em",
      },
    },
    tailwindcss: {},
    autoprefixer: {},
  },
};
```

- [ ] **Step 3: main.tsx に MantineProvider を追加**

import 順が透過維持の要: `@mantine/core/styles.css` → `@/app/index.css`（後勝ちで body の rgba 背景が Mantine のグローバル body 背景を上書きする）。

```tsx
import { StrictMode } from "react";
import { createRoot } from "react-dom/client";
import { MantineProvider } from "@mantine/core";
// Mantine のグローバル CSS より index.css を後に読み、CEF 透過の body 背景を勝たせる
// Load index.css after Mantine globals so the CEF-transparent body background wins
import "@mantine/core/styles.css";
import App from "@/app/App";
import "@/app/index.css";
import { setToastSink } from "@/bridge/notify";
import { emitToast } from "@/features/toast/toastStore";

// bridge の通知 sink に toast store を注入（bridge→features の逆依存を作らない）
// Inject the toast store into the bridge notify sink (avoids a bridge→features back-dependency)
setToastSink(emitToast);

createRoot(document.getElementById("root")!).render(
  <StrictMode>
    <MantineProvider defaultColorScheme="dark">
      <App />
    </MantineProvider>
  </StrictMode>
);
```

- [ ] **Step 4: 全検証を実行**

```bash
pnpm test && pnpm test:e2e && pnpm build
```

Expected: すべて PASS（既存 UI は Tailwind のまま無変更。body 背景が rgba(17,17,17,0.6) のままであることを `pnpm dev` でブラウザ確認するのが望ましい）。

- [ ] **Step 5: Commit**

```bash
git add package.json pnpm-lock.yaml postcss.config.js src/main.tsx
git commit -m "feat(webui): Mantine v8 基盤導入（Tailwind と一時共存）"
```

---

### Task 2: shared/ui 移行（ItemSlot / FluidSlot / ItemIcon / ProgressArrow / SlotGrid 新設）

**Files:**
- Modify: `src/shared/ui/ItemSlot/index.tsx`
- Modify: `src/shared/ui/ItemSlot/style.module.css`
- Modify: `src/shared/ui/FluidSlot/index.tsx`
- Modify: `src/shared/ui/FluidSlot/style.module.css`
- Modify: `src/shared/ui/ItemIcon.tsx`
- Create: `src/shared/ui/ItemIcon.module.css`
- Modify: `src/shared/ui/ProgressArrow/index.tsx`
- Create: `src/shared/ui/ProgressArrow/style.module.css`
- Create: `src/shared/ui/SlotGrid/index.tsx`
- Create: `src/shared/ui/SlotGrid/style.module.css`
- Modify: `src/shared/ui/index.ts`
- Test: `e2e/tests/hotbar.spec.ts`（選択状態を `data-selected` 化）
- Test: `e2e/tests/fluidSlot.spec.ts`（ツールチップを hover 検証化）

**Interfaces:**
- Consumes: Task 1 の MantineProvider（Tooltip が必要とする）
- Produces:
  - `ItemSlot` props 変更なし。選択状態は DOM 上 `data-selected="true"` 属性で表現（e2e 契約）
  - `SlotGrid`: `{ children: ReactNode; cols?: number(省略時9); testId?: string; onWheel?: (e: WheelEvent<HTMLDivElement>) => void; className?: string }` — Task 3/4/5 がスロットグリッドに使用
  - ツールチップは Mantine Tooltip（hover 時のみ DOM に載る。常時 attach 前提の検証は不可）

- [ ] **Step 1: e2e を新契約に更新（先に失敗させる）**

`e2e/tests/hotbar.spec.ts` — `toHaveClass(/border-yellow-400/)` を全箇所 `data-selected` 属性検証に置換:

```ts
// 変更前: await expect(hotbarSlots(page).nth(0)).toHaveClass(/border-yellow-400/);
// 変更後:
await expect(hotbarSlots(page).nth(0)).toHaveAttribute("data-selected", "true");
// 変更前: await expect(hotbarSlots(page).nth(1)).not.toHaveClass(/border-yellow-400/);
// 変更後:
await expect(hotbarSlots(page).nth(1)).not.toHaveAttribute("data-selected", "true");
```

（15,16,22,38,39行目の5箇所。テスト名の「（border-yellow-400）」も「（data-selected）」へ変更）

`e2e/tests/fluidSlot.spec.ts` 33行目 — Tooltip は hover 時のみマウントされるため:

```ts
// 変更前: await expect(page.getByText("Water")).toBeAttached();
// 変更後: hover でツールチップを開いてから可視検証する
// Hover to open the tooltip, then assert visibility
await page.getByTestId("fluid-slot").first().hover();
await expect(page.getByText("Water")).toBeVisible();
```

- [ ] **Step 2: e2e を実行して失敗を確認**

```bash
pnpm test:e2e
```

Expected: hotbar / fluidSlot の該当テストが FAIL（`data-selected` 属性・hover ツールチップ未実装のため）。

- [ ] **Step 3: SlotGrid を新規作成**

`src/shared/ui/SlotGrid/index.tsx`:

```tsx
import type { ReactNode, WheelEvent } from "react";
import styles from "./style.module.css";

type Props = {
  children: ReactNode;
  // 省略時は uGUI 標準の 9 列
  // Defaults to the uGUI-standard 9 columns when omitted
  cols?: number;
  testId?: string;
  onWheel?: (e: WheelEvent<HTMLDivElement>) => void;
  className?: string;
};

// スロットを固定幅セルで並べる共通グリッド（inventory/hotbar/block/itemList で共用）
// Shared fixed-cell slot grid used by inventory, hotbar, block, and item list views
export default function SlotGrid({ children, cols, testId, onWheel, className }: Props) {
  return (
    <div
      data-testid={testId}
      className={className ? `${styles.grid} ${className}` : styles.grid}
      style={{ gridTemplateColumns: `repeat(${cols ?? 9}, max-content)` }}
      onWheel={onWheel}
    >
      {children}
    </div>
  );
}
```

`src/shared/ui/SlotGrid/style.module.css`:

```css
/* スロットセルを詰めて並べる。列数は inline style の repeat で可変 */
/* Packs slot cells tightly; the column count varies via the inline repeat style */
.grid {
  display: grid;
  gap: 0.25rem;
  width: fit-content;
}
```

- [ ] **Step 4: ItemSlot を CSS Module + Mantine Tooltip 化**

`src/shared/ui/ItemSlot/style.module.css`（全置換）:

```css
/* 48px 固定のアイテムスロット。選択状態は data-selected 属性で表現（e2e 契約） */
/* Fixed 48px item slot; selection is expressed via the data-selected attribute (e2e contract) */
.slot {
  position: relative;
  width: 3rem;
  height: 3rem;
  border: 1px solid var(--mantine-color-dark-4);
  border-radius: var(--mantine-radius-sm);
  background-color: var(--mantine-color-dark-8);
  user-select: none;
}

.slot[data-selected="true"] {
  border-color: var(--mantine-color-yellow-4);
}

.icon {
  width: 100%;
  height: 100%;
  object-fit: contain;
  padding: 2px;
}

.count {
  position: absolute;
  bottom: 0;
  right: 2px;
  font-size: 12px;
  font-weight: 700;
  color: var(--mantine-color-green-3);
  text-shadow: 0 1px 2px rgba(0, 0, 0, 0.8);
}
```

`src/shared/ui/ItemSlot/index.tsx`（全置換）:

```tsx
import type { MouseEvent } from "react";
import { Tooltip } from "@mantine/core";
import ItemIcon from "../ItemIcon";
import styles from "./style.module.css";

type Props = {
  itemId: number;
  // count 省略時は個数バッジを表示せず、itemId>0 ならアイコンのみ表示する
  // When count is omitted, the count badge is hidden and the icon shows for itemId>0
  count?: number;
  name?: string;
  selected?: boolean;
  onLeftDown?: (shiftKey: boolean) => void;
  onRightDown?: () => void;
  onDoubleClick?: () => void;
};

// アイコン・個数・ホバーツールチップ付きの汎用アイテムスロット
// Generic item slot with icon, count, and a hover tooltip
export default function ItemSlot({ itemId, count, name, selected, onLeftDown, onRightDown, onDoubleClick }: Props) {
  const onMouseDown = (e: MouseEvent) => {
    e.preventDefault();
    if (e.button === 0) onLeftDown?.(e.shiftKey);
    if (e.button === 2) onRightDown?.();
  };

  const hasItem = itemId > 0 && (count === undefined || count > 0);

  return (
    // Tooltip は子要素をラップせず cloneElement するため DOM 構造（grid > div）は不変
    // Tooltip clones the child without a wrapper, keeping the grid > div DOM shape intact
    <Tooltip label={name} disabled={!hasItem || !name}>
      <div
        className={styles.slot}
        data-selected={selected ? "true" : undefined}
        onMouseDown={onMouseDown}
        onDoubleClick={onDoubleClick}
        onContextMenu={(e) => e.preventDefault()}
      >
        {hasItem ? (
          <>
            <ItemIcon itemId={itemId} alt={name ?? `item ${itemId}`} className={styles.icon} />
            {count !== undefined ? <span className={styles.count}>{count}</span> : null}
          </>
        ) : null}
      </div>
    </Tooltip>
  );
}
```

- [ ] **Step 5: FluidSlot を CSS Module + Mantine Tooltip 化**

`src/shared/ui/FluidSlot/style.module.css`（全置換）:

```css
/* 48px 固定の流体スロット。縦フィルの高さと色は inline style（動的値） */
/* Fixed 48px fluid slot; the vertical fill height/color are dynamic inline styles */
.slot {
  position: relative;
  width: 3rem;
  height: 3rem;
  border: 1px solid var(--mantine-color-dark-4);
  border-radius: var(--mantine-radius-sm);
  background-color: var(--mantine-color-dark-8);
  overflow: hidden;
  user-select: none;
}

.fill {
  position: absolute;
  bottom: 0;
  left: 0;
  width: 100%;
}

.amount {
  position: absolute;
  bottom: 0;
  right: 2px;
  font-size: 12px;
  font-weight: 700;
  color: var(--mantine-color-white);
  text-shadow: 0 1px 2px rgba(0, 0, 0, 0.8);
}
```

`src/shared/ui/FluidSlot/index.tsx`（全置換）:

```tsx
import { Tooltip } from "@mantine/core";
import type { FluidSlotData } from "@/bridge/payloadTypes";
import { formatAmount, fillRatio } from "./fluidLogic";
import styles from "./style.module.css";

// fluidId から決定的に色相を導き、液体ごとに安定した色を割り当てる（アイコン代替）
// Derive a deterministic hue from fluidId so each fluid gets a stable color (icon substitute)
function fluidColor(fluidId: number): string {
  const hue = (fluidId * 47) % 360;
  return `hsl(${hue}, 70%, 45%)`;
}

// 色ボックス/量/ホバー名を持つ汎用流体スロット。uGUI FluidSlotView 相当
// Generic fluid slot (color box, amount, hover name); mirrors uGUI FluidSlotView
export default function FluidSlot({ fluid }: { fluid: FluidSlotData }) {
  const hasFluid = fluid.fluidId > 0 && fluid.amount > 0;

  return (
    <Tooltip label={fluid.name} disabled={!hasFluid || !fluid.name}>
      <div data-testid="fluid-slot" className={styles.slot}>
        {hasFluid ? (
          <>
            {/* amount/capacity に応じた下からの縦フィル */}
            {/* Vertical fill rising from the bottom by amount/capacity */}
            <div
              className={styles.fill}
              style={{ height: `${fillRatio(fluid.amount, fluid.capacity) * 100}%`, backgroundColor: fluidColor(fluid.fluidId) }}
            />
            <span className={styles.amount}>{formatAmount(fluid.amount)}</span>
          </>
        ) : null}
      </div>
    </Tooltip>
  );
}
```

- [ ] **Step 6: ItemIcon のフォールバック表示を CSS Module 化**

`src/shared/ui/ItemIcon.module.css`（新規）:

```css
/* アイコン読み込み失敗時の #id フォールバックラベル */
/* Fallback #id label shown when the icon image fails to load */
.fallback {
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 10px;
  color: var(--mantine-color-dark-2);
}
```

`src/shared/ui/ItemIcon.tsx` — フォールバック span の className を変更（他は現状維持）:

```tsx
import { useState } from "react";
import styles from "./ItemIcon.module.css";

type Props = {
  itemId: number;
  alt?: string;
  className?: string;
};

// アイコン画像と読み込み失敗時の #id フォールバックを共通化する
// Shared item icon with the #id fallback for load failures
export default function ItemIcon({ itemId, alt, className }: Props) {
  // アイコン読み込み失敗を itemId 単位で記録し、ID ラベル表示に切り替える
  // Track icon load failures per itemId and fall back to the id label
  const [erroredItemId, setErroredItemId] = useState<number | null>(null);

  if (erroredItemId === itemId) {
    return <span className={`${styles.fallback} ${className ?? ""}`}>#{itemId}</span>;
  }

  return (
    <img
      src={`/api/icons/${itemId}.png`}
      alt={alt ?? `item ${itemId}`}
      className={className}
      draggable={false}
      onError={() => setErroredItemId(itemId)}
    />
  );
}
```

- [ ] **Step 7: ProgressArrow を CSS Module 化**

`src/shared/ui/ProgressArrow/style.module.css`（新規）:

```css
/* 横向き進捗矢印のトラックとフィル。フィル幅は inline style（動的値） */
/* Track and fill of the horizontal progress arrow; the fill width is a dynamic inline style */
.track {
  position: relative;
  width: 4rem;
  height: 0.75rem;
  border: 1px solid var(--mantine-color-dark-4);
  border-radius: var(--mantine-radius-sm);
  background-color: var(--mantine-color-dark-6);
  overflow: hidden;
}

.fill {
  position: absolute;
  top: 0;
  bottom: 0;
  left: 0;
  background-color: var(--mantine-color-green-6);
}
```

`src/shared/ui/ProgressArrow/index.tsx`（全置換）:

```tsx
import styles from "./style.module.css";

// 進捗値を 0..1 に丸める（uGUI slider.value と同じ範囲）。NaN は 0 扱い
// Clamp the progress value to 0..1 (same range as uGUI slider.value); NaN treated as 0
function clamp01(n: number): number {
  if (Number.isNaN(n)) return 0;
  if (n < 0) return 0;
  if (n > 1) return 1;
  return n;
}

// 0..1 を幅 % で満たす横向き進捗矢印。uGUI ProgressArrowView 相当
// Horizontal progress arrow filling by width %; mirrors uGUI ProgressArrowView
export default function ProgressArrow({ value }: { value: number }) {
  const percent = `${clamp01(value) * 100}%`;
  return (
    <div data-testid="progress-arrow" className={styles.track}>
      <div className={styles.fill} style={{ width: percent }} />
    </div>
  );
}
```

- [ ] **Step 8: shared/ui の index.ts に SlotGrid を追加**

`src/shared/ui/index.ts` に追記:

```ts
export { default as SlotGrid } from "./SlotGrid";
```

- [ ] **Step 9: 全検証**

```bash
pnpm test && pnpm test:e2e && pnpm build
```

Expected: すべて PASS（Step 2 で失敗した hotbar / fluidSlot テストが通る）。

- [ ] **Step 10: Commit**

```bash
git add src/shared/ui e2e/tests/hotbar.spec.ts e2e/tests/fluidSlot.spec.ts
git commit -m "feat(webui): shared/ui を Mantine+CSS Modules 化、SlotGrid 新設"
```

---

### Task 3: inventory 機能移行（InventoryPanel / GrabOverlay）

**Files:**
- Modify: `src/features/inventory/InventoryPanel/index.tsx`
- Create: `src/features/inventory/InventoryPanel/style.module.css`
- Modify: `src/features/inventory/InventoryPanel/GrabOverlay.tsx`
- Create: `src/features/inventory/InventoryPanel/GrabOverlay.module.css`
- Test: `e2e/tests/hotbar.spec.ts`（grid セレクタ→`hotbar-grid`）
- Test: `e2e/tests/inventory.spec.ts`（grid セレクタ→`main-grid`、`.fixed.z-40`→`grab-overlay`）
- Test: `e2e/tests/blockInventory.spec.ts`（`.fixed.z-40`→`grab-overlay`）

**Interfaces:**
- Consumes: `SlotGrid`（Task 2。`testId` / `onWheel` / `className` props）、Mantine `Stack, Group, Button, Title, Text`
- Produces: e2e 契約 `data-testid="main-grid"` / `data-testid="hotbar-grid"` / `data-testid="grab-overlay"`。ロジック（onLeftDown / onRightDown / onDoubleClick / directMove / wheel / キー選択）は無変更

- [ ] **Step 1: e2e セレクタを testid に更新（先に失敗させる）**

`e2e/tests/hotbar.spec.ts` 8行目のヘルパー:

```ts
// 変更前: page.locator(".grid.grid-cols-9").nth(1).locator("> div");
// 変更後:
const hotbarSlots = (page: Page) => page.getByTestId("hotbar-grid").locator("> div");
```

`e2e/tests/inventory.spec.ts`:

```ts
// 16,28,52行目 変更前: page.locator(".grid.grid-cols-9 > div").first()
// 変更後:
const firstSlot = page.getByTestId("main-grid").locator("> div").first();
// 20行目 変更前: await expect(page.locator(".fixed.z-40")).toBeVisible();
// 変更後:
await expect(page.getByTestId("grab-overlay")).toBeVisible();
```

`e2e/tests/blockInventory.spec.ts` 47行目:

```ts
// 変更前: await expect(page.locator(".fixed.z-40")).toBeVisible();
// 変更後:
await expect(page.getByTestId("grab-overlay")).toBeVisible();
```

- [ ] **Step 2: e2e 実行で失敗確認**

```bash
pnpm test:e2e
```

Expected: hotbar / inventory / blockInventory の該当テストが FAIL（testid 未実装）。

- [ ] **Step 3: InventoryPanel の描画部を Mantine + SlotGrid 化**

`src/features/inventory/InventoryPanel/style.module.css`（新規）:

```css
/* ホットバーを下段グリッド領域の中央へ配置する外枠と、枠線付きの内枠 */
/* Outer wrapper centering the hotbar in the bottom grid area, plus its bordered frame */
.hotbarArea {
  grid-area: hotbar;
  display: flex;
  justify-content: center;
}

.hotbarFrame {
  border: 1px solid var(--mantine-color-dark-3);
  border-radius: var(--mantine-radius-sm);
  background-color: rgba(31, 31, 31, 0.6);
  padding: 0.25rem;
}
```

`src/features/inventory/InventoryPanel/index.tsx` — import 変更と JSX 置換（ロジック部 10〜118 行は無変更）:

import 追加/変更:

```tsx
import { Button, Group, Stack, Text, Title } from "@mantine/core";
import { ItemSlot, SlotGrid } from "@/shared/ui";
import styles from "./style.module.css";
```

`if (!inventory)` の早期 return:

```tsx
  if (!inventory) {
    return <Text size="sm" c="dimmed" style={{ gridArea: "inv" }}>connecting...</Text>;
  }
```

最終 return ブロック（全置換）:

```tsx
  return (
    <>
      <Stack gap="sm" style={{ gridArea: "inv" }}>
        <Group gap="sm">
          <Title order={2} size="h4">Inventory</Title>
          <Button variant="default" size="compact-sm" onClick={() => void dispatchAction("inventory.sort", {})}>
            Sort
          </Button>
        </Group>
        <SlotGrid testId="main-grid">
          {inventory.mainSlots.map((s, i) => renderSlot("main", i, s))}
        </SlotGrid>
      </Stack>
      {/* ホットバーは uGUI と同様に画面下段の中央へ独立配置 */}
      {/* The hotbar sits independently at the bottom center, matching uGUI */}
      <div className={styles.hotbarArea}>
        <SlotGrid testId="hotbar-grid" className={styles.hotbarFrame} onWheel={onHotbarWheel}>
          {inventory.hotbarSlots.map((s, i) => renderSlot("hotbar", i, s))}
        </SlotGrid>
      </div>
      <GrabOverlay grab={inventory.grab} />
    </>
  );
```

- [ ] **Step 4: GrabOverlay を CSS Module 化**

`src/features/inventory/InventoryPanel/GrabOverlay.module.css`（新規）:

```css
/* カーソル追従の 48px オーバーレイ。座標は inline style（動的値） */
/* Cursor-following 48px overlay; coordinates are dynamic inline styles */
.overlay {
  pointer-events: none;
  position: fixed;
  z-index: 40;
  width: 3rem;
  height: 3rem;
}
```

`src/features/inventory/InventoryPanel/GrabOverlay.tsx` の return 部:

```tsx
import styles from "./GrabOverlay.module.css";
```

```tsx
  // 追従位置はカーソル座標の動的値なので inline style（module 化対象外）
  // Follow position is a dynamic cursor value, so inline style (not module-ized)
  return (
    <div data-testid="grab-overlay" className={styles.overlay} style={{ left: mousePos.x - 24, top: mousePos.y - 24 }}>
      <ItemSlot itemId={grab.itemId} count={grab.count} />
    </div>
  );
```

- [ ] **Step 5: 全検証**

```bash
pnpm test && pnpm test:e2e && pnpm build
```

Expected: すべて PASS。

- [ ] **Step 6: Commit**

```bash
git add src/features/inventory e2e/tests/hotbar.spec.ts e2e/tests/inventory.spec.ts e2e/tests/blockInventory.spec.ts
git commit -m "feat(webui): inventory を Mantine+SlotGrid 化、e2e を testid セレクタに移行"
```

---

### Task 4: recipe 機能移行（RecipeViewer / Craft / Machine / Pager / Header / ItemList）

**Files:**
- Modify: `src/features/recipe/RecipeViewer.tsx`
- Create: `src/features/recipe/RecipeViewer.module.css`
- Modify: `src/features/recipe/CraftRecipeView.tsx`
- Modify: `src/features/recipe/MachineRecipeView.tsx`
- Modify: `src/features/recipe/RecipePager.tsx`
- Modify: `src/features/recipe/ItemHeader.tsx`
- Modify: `src/features/recipe/ItemListPanel.tsx`
- Test: `e2e/tests/recipe.spec.ts`（`.grid > div`→`item-list-grid`）

**Interfaces:**
- Consumes: `SlotGrid`（`cols={5}` / `testId="item-list-grid"`）、Mantine `Tabs, Stack, Group, Button, ActionIcon, ScrollArea, Text, Title, Box`
- Produces: e2e 契約 `data-testid="item-list-grid"`。タブは Mantine Tabs（`role="tab"`）になるが e2e はタブを直接選択していないため契約変更なし。`key={selectedItemId}` 再マウント契約・`clampIndex`・`craftable` は無変更

- [ ] **Step 1: e2e セレクタ更新（先に失敗させる）**

`e2e/tests/recipe.spec.ts` 10〜13行目:

```ts
// 変更前: page.getByRole("heading", { name: "Items" }).locator("..").locator(".grid > div")
// 変更後:
const firstItem = page.getByTestId("item-list-grid").locator("> div").first();
```

```bash
pnpm test:e2e
```

Expected: recipe.spec が FAIL（`item-list-grid` 未実装）。

- [ ] **Step 2: ItemListPanel を Stack + ScrollArea + SlotGrid 化（全置換）**

```tsx
import { ScrollArea, Stack, Text, Title } from "@mantine/core";
import { useTopic, Topics } from "@/bridge";
import { useItemMaster } from "@/bridge/useItemMaster";
import { useUiStore } from "@/app/uiStore";
import { ItemSlot, SlotGrid } from "@/shared/ui";

// 右カラム: 表示対象アイテムの一覧（uGUI の ItemListView 準拠）。クリックで中央にレシピ表示
// Right column: list of viewable items, like uGUI's ItemListView; click shows recipes in the center
export default function ItemListPanel() {
  const selectedItemId = useUiStore((s) => s.selectedItemId);
  const onSelect = useUiStore((s) => s.setSelectedItem);
  const itemList = useTopic(Topics.itemList);
  const itemMaster = useItemMaster();

  return (
    <Stack gap="sm" style={{ gridArea: "items" }}>
      <Title order={2} size="h4">Items</Title>
      {itemList ? (
        <ScrollArea.Autosize mah="70vh" type="auto" offsetScrollbars>
          <SlotGrid cols={5} testId="item-list-grid">
            {itemList.itemIds.map((id) => (
              <ItemSlot
                key={id}
                itemId={id}
                name={itemMaster?.get(id)?.name}
                selected={id === selectedItemId}
                onLeftDown={() => onSelect(id)}
              />
            ))}
          </SlotGrid>
        </ScrollArea.Autosize>
      ) : (
        <Text size="sm" c="dimmed">connecting...</Text>
      )}
    </Stack>
  );
}
```

- [ ] **Step 3: RecipeViewer を Stack + Tabs 化**

`src/features/recipe/RecipeViewer.module.css`（新規）:

```css
/* タブ見出し内のブロックアイコン（20px） */
/* Block icon inside the tab label (20px) */
.tabIcon {
  width: 1.25rem;
  height: 1.25rem;
  object-fit: contain;
}
```

`RecipeViewer.tsx` — import 追加:

```tsx
import { Stack, Tabs, Text, Title } from "@mantine/core";
import styles from "./RecipeViewer.module.css";
```

外側コンポーネントの return（置換）:

```tsx
  return (
    <Stack gap="sm" style={{ gridArea: "viewer", minWidth: 0 }}>
      <Title order={2} size="h4">Recipe</Title>
      {!loaded ? (
        <Text size="sm" c="dimmed">connecting...</Text>
      ) : selectedItemId === null ? (
        <Text size="sm" c="dimmed">右のアイテムリストからアイテムを選択してください</Text>
      ) : (
        // key={selectedItemId} の再マウントで tabKey/recipeIndex をリセットする契約は維持
        // Keep the contract: remount via key={selectedItemId} resets tabKey/recipeIndex
        <RecipeContent
          key={selectedItemId}
          itemId={selectedItemId}
          recipes={recipes}
          machineRecipes={machineRecipes}
          inventory={inventory}
          itemMaster={itemMaster}
          onSelect={onSelect}
        />
      )}
    </Stack>
  );
```

`RecipeContent` の「レシピなし」分岐と最終 return（置換。ロジック部は無変更）:

```tsx
  if (activeTab === null) {
    return (
      <Stack gap="sm">
        <ItemHeader itemId={itemId} name={itemName} />
        <Text size="sm" c="dimmed">このアイテムのレシピはありません</Text>
      </Stack>
    );
  }

  // サーバーの OneClickCraft は main+hotbar のみ参照するため、grab は所持数に含めない
  // The server's OneClickCraft only consults main+hotbar, so grab is excluded from the tally
  const counts = buildOwnedCounts(inventory);

  return (
    <Stack gap="sm">
      <ItemHeader itemId={itemId} name={itemName} />
      {tabs.length > 1 ? (
        <Tabs
          variant="pills"
          value={activeTab.key}
          onChange={(v) => {
            if (v === null) return;
            setTabKey(v);
            setRecipeIndex(0);
          }}
        >
          <Tabs.List>
            {tabs.map((t) => (
              <Tabs.Tab
                key={t.key}
                value={t.key}
                leftSection={t.blockItemId !== null ? <ItemIcon itemId={t.blockItemId} className={styles.tabIcon} /> : undefined}
              >
                {t.label}
              </Tabs.Tab>
            ))}
          </Tabs.List>
        </Tabs>
      ) : null}
      {activeTab.blockItemId === null ? (
        <CraftRecipeView
          recipes={craftRecipes}
          recipeIndex={recipeIndex}
          setRecipeIndex={setRecipeIndex}
          counts={counts}
          itemMaster={itemMaster}
          onSelect={onSelect}
        />
      ) : (
        <MachineRecipeView
          recipes={machineGroups.get(activeTab.blockItemId)!}
          recipeIndex={recipeIndex}
          setRecipeIndex={setRecipeIndex}
          itemMaster={itemMaster}
          onSelect={onSelect}
        />
      )}
    </Stack>
  );
```

- [ ] **Step 4: CraftRecipeView / MachineRecipeView / RecipePager / ItemHeader を Mantine 化**

`CraftRecipeView.tsx` の return（置換。import に `Box, Button, Group, Stack, Text` を追加）:

```tsx
  return (
    <Stack gap="xs">
      <RecipePager index={index} count={recipes.length} setIndex={setRecipeIndex} />
      <Group gap={4} align="center" wrap="wrap">
        {recipe.requiredItems.map((r, i) => (
          // 所持数不足の素材は 40% 透過で強調を落とす（uGUI 準拠）
          // Dim insufficient materials to 40% opacity, matching uGUI
          <Box key={i} opacity={(counts.get(r.itemId) ?? 0) >= r.count ? 1 : 0.4}>
            <ItemSlot itemId={r.itemId} count={r.count} name={itemMaster?.get(r.itemId)?.name} onLeftDown={() => onSelect(r.itemId)} />
          </Box>
        ))}
        <Text c="dimmed" mx="xs">→</Text>
        <ItemSlot itemId={recipe.resultItemId} count={recipe.resultCount} name={itemMaster?.get(recipe.resultItemId)?.name} />
        <Button color="blue" size="sm" ml="sm" disabled={!isCraftable} onClick={onCraft}>
          Craft
        </Button>
      </Group>
    </Stack>
  );
```

`MachineRecipeView.tsx` の return（置換。import に `Group, Stack, Text` を追加）:

```tsx
  return (
    <Stack gap="xs">
      <RecipePager index={index} count={recipes.length} setIndex={setRecipeIndex} />
      <Group gap={4} align="center" wrap="wrap">
        {recipe.inputItems.map((r, i) => (
          <ItemSlot key={i} itemId={r.itemId} count={r.count} name={itemMaster?.get(r.itemId)?.name} onLeftDown={() => onSelect(r.itemId)} />
        ))}
        <Text c="dimmed" mx="xs">→</Text>
        <Stack gap={0} align="center">
          <ItemSlot itemId={recipe.blockItemId} name={recipe.blockName} onLeftDown={() => onSelect(recipe.blockItemId)} />
          <Text fz={10} c="dimmed" maw="4rem" truncate="end">{recipe.blockName}</Text>
        </Stack>
        <Text c="dimmed" mx="xs">→</Text>
        {recipe.outputItems.map((r, i) => (
          <ItemSlot key={i} itemId={r.itemId} count={r.count} name={itemMaster?.get(r.itemId)?.name} onLeftDown={() => onSelect(r.itemId)} />
        ))}
      </Group>
    </Stack>
  );
```

`RecipePager.tsx`（全置換）:

```tsx
import { ActionIcon, Group, Text } from "@mantine/core";

// 複数レシピの前後送りページャ（< i/n >）
// Pager for stepping through multiple recipes (< i/n >)
export default function RecipePager({
  index,
  count,
  setIndex,
}: {
  index: number;
  count: number;
  setIndex: (i: number) => void;
}) {
  if (count <= 1) return null;
  return (
    <Group gap="xs">
      <ActionIcon variant="default" size="sm" aria-label="前のレシピ" onClick={() => setIndex((index + count - 1) % count)}>
        &lt;
      </ActionIcon>
      <Text size="sm" c="dimmed">
        {index + 1}/{count}
      </Text>
      <ActionIcon variant="default" size="sm" aria-label="次のレシピ" onClick={() => setIndex((index + 1) % count)}>
        &gt;
      </ActionIcon>
    </Group>
  );
}
```

`ItemHeader.tsx`（全置換）:

```tsx
import { Group, Text } from "@mantine/core";
import { ItemSlot } from "@/shared/ui";

// 選択アイテムのアイコン+名前ヘッダ
// Icon + name header for the selected item
export default function ItemHeader({ itemId, name }: { itemId: number; name: string }) {
  return (
    <Group gap="xs">
      <ItemSlot itemId={itemId} name={name} />
      <Text>{name}</Text>
    </Group>
  );
}
```

- [ ] **Step 5: 全検証**

```bash
pnpm test && pnpm test:e2e && pnpm build
```

Expected: すべて PASS（recipe.spec の Craft ボタンは `getByRole("button", { name: "Craft" })` のままヒットする）。

- [ ] **Step 6: Commit**

```bash
git add src/features/recipe e2e/tests/recipe.spec.ts
git commit -m "feat(webui): recipe 機能を Mantine（Tabs/ScrollArea/Button）に移行"
```

---

### Task 5: blockInventory 機能移行（Panel / BlockItemGrid / Tank / Chest / Generic）

**Files:**
- Modify: `src/features/blockInventory/BlockInventoryPanel.tsx`
- Create: `src/features/blockInventory/style.module.css`
- Modify: `src/features/blockInventory/BlockItemGrid.tsx`
- Modify: `src/features/blockInventory/TankInventory.tsx`

**Interfaces:**
- Consumes: `SlotGrid`（`testId` 透過）、Mantine `Paper, Title, Group`
- Produces: e2e 契約は現状維持（`data-testid="block-inventory"` / `"chest-grid"` / `"generic-block-grid"` / `"tank-body"`、グリッド直下が ItemSlot の div）。`BlockInteractionContext` 契約は無変更

- [ ] **Step 1: BlockInventoryPanel を Paper + CSS Module 化**

`src/features/blockInventory/style.module.css`（新規）:

```css
/* 画面中央上部の固定パネル。grab(z-40)/toast(z-300) の下、progress(z-20) の上 */
/* Fixed top-center panel; below grab (z-40) and toasts (z-300), above progress (z-20) */
.panel {
  position: fixed;
  left: 50%;
  top: 6rem;
  transform: translateX(-50%);
  z-index: 30;
}
```

`BlockInventoryPanel.tsx` の return（置換。import に `Paper, Title` と `styles` を追加）:

```tsx
import { Paper, Title } from "@mantine/core";
import styles from "./style.module.css";
```

```tsx
  return (
    <BlockInteractionContext.Provider value={interaction}>
      <Paper data-testid="block-inventory" className={styles.panel} p="md" withBorder bg="dark.6" c="dark.1">
        <Title order={2} size="h4" mb="sm">{data.blockName}</Title>
        <Body data={data} />
      </Paper>
    </BlockInteractionContext.Provider>
  );
```

- [ ] **Step 2: BlockItemGrid を SlotGrid 化**

`BlockItemGrid.tsx` の return（置換。import を `import { ItemSlot, SlotGrid } from "@/shared/ui";` に変更）:

```tsx
  return (
    <SlotGrid testId={testId}>
      {itemSlots.map((slot, index) => (
        <ItemSlot
          key={index}
          itemId={slot.itemId}
          count={slot.count}
          name={resolveName(slot.itemId)}
          onLeftDown={() => onLeftDown(index, slot)}
        />
      ))}
    </SlotGrid>
  );
```

- [ ] **Step 3: TankInventory を Group 化**

`TankInventory.tsx` の return（置換。import に `Group` を追加）:

```tsx
import { Group } from "@mantine/core";
```

```tsx
  return (
    <Group data-testid="tank-body" gap="xs" align="center">
      {/* 各流体スロットを横並びで描画 */}
      {/* Render each fluid slot in a row */}
      {data.fluidSlots.map((fluid, i) => (
        <FluidSlot key={i} fluid={fluid} />
      ))}
      {/* progress が非 null のときだけ加工進捗の矢印を表示 */}
      {/* Show the processing progress arrow only when progress is non-null */}
      {data.progress != null ? <ProgressArrow value={data.progress} /> : null}
    </Group>
  );
```

注意: Mantine Group の直下子要素はそのまま DOM に並ぶため、`fluidSlot.spec.ts` の `tank-body` 配下検証・`chest-grid > div` 検証は既存のまま通る（ChestInventory / GenericBlockInventory は BlockItemGrid 委譲のみで変更不要）。

- [ ] **Step 4: 全検証**

```bash
pnpm test && pnpm test:e2e && pnpm build
```

Expected: すべて PASS（blockInventory.spec / fluidSlot.spec 含む）。

- [ ] **Step 5: Commit**

```bash
git add src/features/blockInventory
git commit -m "feat(webui): blockInventory を Mantine（Paper/Group）+SlotGrid に移行"
```

---

### Task 6: modal 機能移行（Mantine Modal compound + buttonColor）

**Files:**
- Modify: `src/features/modal/modalLogic.ts`（`buttonClass` → `buttonColor`）
- Modify: `src/features/modal/ModalHost.tsx`
- Test: `src/features/modal/modalLogic.test.ts`

**Interfaces:**
- Consumes: Mantine `Modal.Root / Modal.Overlay / Modal.Content / Modal.Body, Title, Text, Button`
- Produces: `buttonColor(variant: ModalRequest["variant"]): "red" | "blue"`。e2e 契約 `data-testid="modal"` / `"modal-backdrop"` / `"modal-button"`、背景クリック＝cancel は維持

- [ ] **Step 1: unit テストを新契約に書き換え（先に失敗させる）**

`src/features/modal/modalLogic.test.ts` の `buttonClass` 検証 2 箇所を置換:

```ts
import { respondPayload, buttonColor } from "./modalLogic";

// variant→Mantine color の対応。confirm は青、error は赤（uGUI の色分け準拠）
// variant→Mantine color mapping; confirm is blue, error is red, mirroring uGUI
test("confirm variant は blue", () => {
  expect(buttonColor("confirm")).toBe("blue");
});

test("error variant は red", () => {
  expect(buttonColor("error")).toBe("red");
});
```

- [ ] **Step 2: unit テスト実行で失敗確認**

```bash
pnpm test
```

Expected: modalLogic.test.ts が FAIL（`buttonColor` 未定義）。

- [ ] **Step 3: modalLogic.ts の buttonClass を buttonColor に置換**

```ts
// variant ごとの Mantine ボタン色を返す。error は赤、confirm は青で uGUI の色分けに対応。
// Returns the Mantine button color per variant; error→red, confirm→blue, mirroring the uGUI styling.
export function buttonColor(variant: ModalRequest["variant"]): "red" | "blue" {
  return variant === "error" ? "red" : "blue";
}
```

（`buttonClass` は削除。`respondPayload` は無変更）

- [ ] **Step 4: ModalHost を Mantine Modal compound 化（全置換）**

```tsx
import { Button, Modal, Text, Title } from "@mantine/core";
import { useTopic, dispatchAction, Topics } from "@/bridge";
import { respondPayload, buttonColor } from "./modalLogic";

// uGUI OneButtonModal の web 版。ui.modal トピックを購読し、要求があれば中央モーダルを描く。
// Web version of uGUI OneButtonModal; subscribes ui.modal and renders a centered modal on request.
export function ModalHost() {
  const data = useTopic(Topics.modal);

  // スナップショット未着、または表示対象が無ければ何も描かない。
  // Render nothing before the first snapshot or when there is no modal to show.
  if (!data || !data.modal) return null;
  const { id, title, message, buttonText, variant } = data.modal;

  // confirm/cancel を host へ送る。オーバーレイクリックは Modal.Root の onClose 経由で cancel。
  // Send confirm/cancel to the host; overlay clicks cancel via Modal.Root's onClose.
  const confirm = () => dispatchAction("ui.modal.respond", respondPayload(id, "confirm"));
  const cancel = () => dispatchAction("ui.modal.respond", respondPayload(id, "cancel"));

  // e2e が同期検証できるようトランジションは無効化する。
  // Disable transitions so e2e can assert synchronously.
  return (
    <Modal.Root opened onClose={() => void cancel()} centered transitionProps={{ duration: 0 }}>
      <Modal.Overlay data-testid="modal-backdrop" backgroundOpacity={0.6} />
      <Modal.Content data-testid="modal" w={320}>
        <Modal.Body p="lg">
          {/* タイトルと本文。uGUI の titleText / descriptionText に対応 */}
          {/* Title and body, mapping to uGUI titleText / descriptionText */}
          <Title order={2} size="h4" mb="xs">{title}</Title>
          <Text size="sm" c="dimmed" mb="lg">{message}</Text>
          <Button data-testid="modal-button" fullWidth color={buttonColor(variant)} onClick={() => void confirm()}>
            {buttonText}
          </Button>
        </Modal.Body>
      </Modal.Content>
    </Modal.Root>
  );
}
```

- [ ] **Step 5: 全検証**

```bash
pnpm test && pnpm test:e2e && pnpm build
```

Expected: すべて PASS。特に modal.spec の3件（表示 / OK=confirm / 背景クリック=cancel）が green。背景クリックは Mantine の `closeOnClickOutside`（デフォルト有効）→ `onClose` → cancel の経路で発火する。

- [ ] **Step 6: Commit**

```bash
git add src/features/modal
git commit -m "feat(webui): modal を Mantine Modal compound に移行、buttonColor 化"
```

---

### Task 7: toast 機能移行（Mantine Notification）

**Files:**
- Modify: `src/features/toast/ToastHost.tsx`
- Create: `src/features/toast/style.module.css`
- Test: `e2e/tests/inventory.spec.ts`（`.fixed.bottom-4.right-4`→`toast-host`）

**Interfaces:**
- Consumes: `useToastStore`（無変更。`emitToast` sink 注入もそのまま）、Mantine `Notification, Stack`
- Produces: e2e 契約 `data-testid="toast-host"`

- [ ] **Step 1: e2e セレクタ更新（先に失敗させる）**

`e2e/tests/inventory.spec.ts` 46行目:

```ts
// 変更前: await expect(page.locator(".fixed.bottom-4.right-4").getByText(/failed/)).toHaveCount(0, { timeout: 2000 });
// 変更後:
await expect(page.getByTestId("toast-host").getByText(/failed/)).toHaveCount(0, { timeout: 2000 });
```

```bash
pnpm test:e2e
```

Expected: inventory.spec の該当テストが FAIL（`toast-host` 未実装。※ toHaveCount(0) は要素不在でも通る実装のため、もし PASS する場合はそのまま Step 2 へ進んでよい）。

- [ ] **Step 2: ToastHost を Notification 化（全置換）**

`src/features/toast/style.module.css`（新規）:

```css
/* 画面右下固定。Mantine Modal(z-200) より上に出すため z-index 300 */
/* Pinned to the bottom-right; z-index 300 to stay above the Mantine Modal (z-200) */
.host {
  position: fixed;
  bottom: 1rem;
  right: 1rem;
  z-index: 300;
}
```

`src/features/toast/ToastHost.tsx`:

```tsx
import { Notification, Stack } from "@mantine/core";
import { useToastStore } from "./toastStore";
import styles from "./style.module.css";

// 画面右下にトーストを表示するホスト。自動消滅は store 側（addToast）で管理
// Toast host pinned to the bottom-right; auto-dismiss is handled in the store (addToast)
export default function ToastHost() {
  const toasts = useToastStore((s) => s.toasts);

  return (
    <Stack gap="xs" className={styles.host} data-testid="toast-host">
      {toasts.map((t) => (
        <Notification key={t.id} color="red" withCloseButton={false} withBorder>
          {t.message}
        </Notification>
      ))}
    </Stack>
  );
}
```

- [ ] **Step 3: 全検証**

```bash
pnpm test && pnpm test:e2e && pnpm build
```

Expected: すべて PASS（toastStore の unit テストは store 無変更のためそのまま通る）。

- [ ] **Step 4: Commit**

```bash
git add src/features/toast e2e/tests/inventory.spec.ts
git commit -m "feat(webui): toast を Mantine Notification に移行"
```

---

### Task 8: progress 機能移行（Mantine Progress compound + percentValue）

**Files:**
- Modify: `src/features/progress/progressLogic.ts`（`percentWidth` → `percentValue`）
- Modify: `src/features/progress/ProgressBar.tsx`
- Create: `src/features/progress/style.module.css`
- Test: `src/features/progress/progressLogic.test.ts`

**Interfaces:**
- Consumes: Mantine `Progress.Root / Progress.Section, Text`
- Produces: `percentValue(n: number): number`（0.4 → 40。clampProgress 経由で 0..100 に収まる）。e2e 契約 `data-testid="progress-bar"` / `"progress-fill"`、`style` 属性に `40%` が含まれること（Mantine Progress.Section は `--progress-section-width: 40%` を inline style に出すため既存の `/40%/` 検証がそのまま通る）

- [ ] **Step 1: unit テストに percentValue を追加（先に失敗させる）**

`src/features/progress/progressLogic.test.ts` — `percentWidth` の既存テストを `percentValue` に書き換え:

```ts
import { clampProgress, percentValue } from "./progressLogic";

// Mantine Progress の value（0..100 の数値）への変換を検証する
// Verifies conversion into the Mantine Progress value (a 0..100 number)
test("0.4 は 40 になる", () => {
  expect(percentValue(0.4)).toBe(40);
});

test("範囲外と NaN はクランプされる", () => {
  expect(percentValue(-1)).toBe(0);
  expect(percentValue(2)).toBe(100);
  expect(percentValue(Number.NaN)).toBe(0);
});
```

（`clampProgress` の既存テストは維持）

- [ ] **Step 2: unit テスト実行で失敗確認**

```bash
pnpm test
```

Expected: progressLogic.test.ts が FAIL（`percentValue` 未定義）。

- [ ] **Step 3: progressLogic.ts の percentWidth を percentValue に置換**

```ts
// 丸めた進捗を Mantine Progress の value へ変換（0.4 → 40）。
// Convert the clamped progress into the Mantine Progress value (0.4 → 40).
export function percentValue(n: number): number {
  return clampProgress(n) * 100;
}
```

（`percentWidth` は削除。`clampProgress` は無変更）

- [ ] **Step 4: ProgressBar を Mantine Progress compound 化（全置換）**

`src/features/progress/style.module.css`（新規）:

```css
/* 画面下部中央の固定トラック。クリックを奪わない表示専用オーバーレイ */
/* Fixed bottom-center track; a display-only overlay that never captures clicks */
.wrapper {
  pointer-events: none;
  position: fixed;
  bottom: 2rem;
  left: 50%;
  transform: translateX(-50%);
  width: 16rem;
  z-index: 20;
}
```

`src/features/progress/ProgressBar.tsx`:

```tsx
import { Progress, Text } from "@mantine/core";
import { useTopic, Topics } from "@/bridge";
import { percentValue } from "./progressLogic";
import styles from "./style.module.css";

// uGUI ProgressBarView を模した表示専用オーバーレイ。visible で Show/Hide を切り替える。
// Display-only overlay mirroring uGUI ProgressBarView; visible toggles Show/Hide.
export function ProgressBar() {
  const data = useTopic(Topics.progress);

  // 初回スナップショット前(null)や非表示時は何も描画しない。
  // Render nothing before the first snapshot (null) or while hidden.
  if (!data || !data.visible) return null;

  // 画面下部中央に固定し、任意ラベル・トラック・割合フィルを重ねる。
  // Pin to the bottom-center, layering the optional label, track, and proportional fill.
  return (
    <div data-testid="progress-bar" className={styles.wrapper}>
      {data.label != null && (
        <Text size="sm" c="dimmed" mb={4}>{data.label}</Text>
      )}
      <Progress.Root size="md" transitionDuration={0}>
        <Progress.Section data-testid="progress-fill" value={percentValue(data.progress)} color="green" />
      </Progress.Root>
    </div>
  );
}
```

- [ ] **Step 5: 全検証**

```bash
pnpm test && pnpm test:e2e && pnpm build
```

Expected: すべて PASS。progress.spec の「フィルの幅が 40% になる」は Progress.Section の inline style（`--progress-section-width: 40%`）で `/40%/` にマッチする。万一マッチしない場合のみ e2e を `toHaveAttribute("style", /40/)` に緩めてよい。

- [ ] **Step 6: Commit**

```bash
git add src/features/progress
git commit -m "feat(webui): progress を Mantine Progress compound に移行、percentValue 化"
```

---

### Task 9: App/Debug 移行と Tailwind 完全撤去

**Files:**
- Modify: `src/app/App.tsx`
- Modify: `src/app/App.module.css`
- Modify: `src/app/DebugActionButton.tsx`
- Modify: `src/app/index.css`
- Modify: `postcss.config.js`
- Modify: `package.json`（pnpm remove 経由）
- Delete: `tailwind.config.ts` / `tailwind.config.js` / `tailwind.config.d.ts`

**Interfaces:**
- Consumes: Mantine `Group, Title, Button`。Task 2〜8 で全 feature の Tailwind クラスが消えていること
- Produces: Tailwind 依存ゼロのビルド

- [ ] **Step 1: App.tsx を Group + Title 化（全置換）**

```tsx
import { lazy, Suspense } from "react";
import { Group, Title } from "@mantine/core";
import { InventoryPanel } from "@/features/inventory";
import { RecipeViewer, ItemListPanel } from "@/features/recipe";
import { ToastHost } from "@/features/toast";
import { ModalHost } from "@/features/modal";
import { ProgressBar } from "@/features/progress";
import { BlockInventoryPanel } from "@/features/blockInventory";
import styles from "./App.module.css";

// dev 専用。static import すると本番バンドルに残るため import.meta.env.DEV 内で lazy 化
// Dev-only; a static import would ship to prod, so lazy-load it inside the import.meta.env.DEV guard
const DebugActionButton = import.meta.env.DEV ? lazy(() => import("./DebugActionButton")) : null;

// uGUI のインベントリ画面準拠の3カラム+下段ホットバーレイアウト
// Three-column layout with a bottom hotbar row, matching the uGUI inventory screen
export default function App() {
  return (
    <div className={styles.layout}>
      <Group gap="md" style={{ gridArea: "header" }}>
        <Title order={1} size="h3">moorestech Web UI</Title>
        {DebugActionButton ? (
          <Suspense fallback={null}>
            <DebugActionButton />
          </Suspense>
        ) : null}
      </Group>
      <InventoryPanel />
      <RecipeViewer />
      <ItemListPanel />
      {/* オーバーレイ系（grid セルでなく fixed/center 配置） */}
      {/* Overlays (fixed/centered, not grid cells) */}
      <BlockInventoryPanel />
      <ModalHost />
      <ProgressBar />
      <ToastHost />
    </div>
  );
}
```

- [ ] **Step 2: App.module.css に padding / min-height を追加**

```css
/* uGUI 準拠の3カラム+下段ホットバー。grid-template-areas が複雑なため module 化 */
/* uGUI-style 3 columns + bottom hotbar; module-ized for the complex grid-template-areas */
.layout {
  display: grid;
  gap: 1.5rem;
  padding: 1rem;
  min-height: 100vh;
  grid-template-areas:
    "header header header"
    "inv viewer items"
    "hotbar hotbar hotbar";
  grid-template-columns: auto 1fr auto;
  grid-template-rows: auto 1fr auto;
}
```

- [ ] **Step 3: DebugActionButton を Mantine Button 化**

```tsx
import { Button } from "@mantine/core";
import { dispatchAction } from "@/bridge/actions";
import { emitToast } from "@/features/toast/toastStore";

// debug.echo を発行して双方向APIの疎通を確認する開発用ボタン
// Dev button that sends debug.echo to verify the bidirectional API
export default function DebugActionButton() {
  const onClick = async () => {
    const ok = await dispatchAction("debug.echo", { hello: "world" });
    if (ok) emitToast("debug.echo ok");
  };

  return (
    <Button variant="default" size="compact-sm" onClick={onClick}>
      Ping Action
    </Button>
  );
}
```

- [ ] **Step 4: index.css から @tailwind を削除**

```css
/* CEF でゲーム画面に重ねるため背景は半透明にする（ゲームが透けて見える） */
/* Semi-transparent background so the game shows through behind the CEF overlay */
/* Mantine のグローバル body 背景（--mantine-color-body）を import 順で上書きする */
/* Overrides Mantine's global body background (--mantine-color-body) via import order */
body {
  margin: 0;
  font-family: system-ui, -apple-system, BlinkMacSystemFont, sans-serif;
  background-color: rgba(17, 17, 17, 0.6);
  color: #eee;
}
```

- [ ] **Step 5: Tailwind パッケージ・設定を撤去**

```bash
pnpm remove tailwindcss autoprefixer
rm tailwind.config.ts tailwind.config.js tailwind.config.d.ts
```

`postcss.config.js` を Mantine のみに:

```js
export default {
  plugins: {
    "postcss-preset-mantine": {},
    "postcss-simple-vars": {
      variables: {
        "mantine-breakpoint-xs": "36em",
        "mantine-breakpoint-sm": "48em",
        "mantine-breakpoint-md": "62em",
        "mantine-breakpoint-lg": "75em",
        "mantine-breakpoint-xl": "88em",
      },
    },
  },
};
```

- [ ] **Step 6: Tailwind 残骸の grep 掃討**

```bash
grep -rn "tailwind" package.json postcss.config.js index.html src e2e ; \
grep -rEn 'className="[^"]*\b(bg-|text-(xs|sm|lg|2xl|gray|white|green)|flex |grid |rounded|border-|p-[0-9]|px-|py-|w-(12|16|64|80|fit|full)|h-(3|12|full)|space-y|gap-[0-9]|fixed|absolute|hidden|group)' src
```

Expected: どちらも 0 件（exit code 1）。ヒットが出たら該当ファイルを本計画の該当タスクの方式（Mantine コンポーネント or CSS Module）で潰す。

- [ ] **Step 7: 全検証（最終）**

```bash
pnpm test && pnpm test:e2e && pnpm build
```

Expected: すべて PASS。`pnpm build` の出力に tailwind 関連の警告が無いこと。可能なら `pnpm dev` + mock host でブラウザ目視確認（3カラムレイアウト、スロット選択、モーダル、トースト、進捗バー、body 透過）。

- [ ] **Step 8: Commit**

```bash
git add -A
git commit -m "feat(webui): Tailwind を完全撤去し Mantine 移行を完了"
```

---

## 検証マトリクス（全タスク共通）

| コマンド | 内容 |
|---|---|
| `pnpm test` | Vitest（純ロジック unit。node 環境） |
| `pnpm test:e2e` | Playwright（mock host 経由の UI 統合） |
| `pnpm build` | `tsc -b && vite build`（型 + バンドル） |

## リスクと逃げ道

- **Mantine Modal の overlay クリック→cancel が発火しない場合**: `Modal.Root` に `closeOnClickOutside`（デフォルト true）が効いているか確認。だめなら `<Modal.Overlay onClick={() => void cancel()}>` を明示追加。
- **Progress.Section の style に "40%" が入らない場合**: e2e を `toHaveAttribute("style", /40/)` へ緩和（Task 8 Step 5 に記載）。
- **Tooltip が e2e のクリックを妨げる場合**: Mantine Tooltip はデフォルト `pointer-events: none` なので通常は問題ない。問題が出たら `events={{ hover: true, focus: false, touch: false }}` と `openDelay={200}` を付与。
- **色味の完全一致は目標にしない**: gray-900/700 等は Mantine dark パレット（dark-8/dark-4 等）への近似置換とする。視覚差の微調整は移行完了後に theme で一括調整する。
