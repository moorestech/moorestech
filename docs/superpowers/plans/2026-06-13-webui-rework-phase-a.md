# moorestech Web UI 刷新 フェーズA 実装計画

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** `moorestech_web/webui` を feature 単位階層 + 型付き protocol + Zustand(UI状態のみ) + vitest/Playwright 2層テストへ刷新する（見た目・挙動は不変＝純リファクタ）。

**Architecture:** 通信境界を `bridge/`（一方向: feature→bridge）に固め、サーバー由来DTOは `bridge/payloadTypes.ts`、Topic/Action 契約は `bridge/protocol.ts` に集約して `useTopic`/`dispatchAction` を型付けする。繊細な分岐ロジックを純粋関数に抽出し vitest で固定、ユーザーフローを Playwright + モックWSホストで固定する。最後にフォルダ移動・CSS module化・dev隔離を行う。

**Tech Stack:** Vite 5 + React 18 + TypeScript 5.7 + **pnpm** / Tailwind 3.4 + CSS Modules / Zustand / vitest / Playwright（mock WS host: `ws`）

**設計書:** `docs/superpowers/specs/2026-06-13-webui-rework-design.md`

**重要な前提:**
- パッケージマネージャは **pnpm**（`pnpm-lock.yaml` 有り）。`npm` を使わない。
- 作業ディレクトリは `moorestech_web/webui`。コマンドは原則ここから実行。
- `.cs` を変更するのはフェーズBのみ。フェーズAは TS/CSS/設定のみ → Unity コンパイルは不要。
- **本計画はフェーズAのみ。** フェーズB（dblclick collect の host側修正・C#変更）は末尾に概要を載せるが、別計画として A 完了後に詳細化する。

## 壊してはいけない契約（全タスク共通・各commitで保全確認）

1. `dispatchAction` の戻り値 `true`=受理であって topic 反映完了ではない。楽観更新を入れない。
2. grab 状態は WS topic 由来。**Zustand に入れない。**
3. `RecipeViewer` の `recipeIndex`/`tabKey` はローカルstate。`key={selectedItemId}` の再マウントでリセットされる契約を維持（store化後も key を外さない）。
4. `InventoryPanel` の `clickGrabHistory` 等 gesture/ref 状態を再マウントで飛ばさない（key/中間ラッパを挟まない）。※フェーズAでは **挙動・ロジック不変・移動のみ**。

---

## Task 0: ツール・依存の導入（zustand / vitest / playwright / @ paths）

**Files:**
- Modify: `moorestech_web/webui/package.json`
- Create: `moorestech_web/webui/vitest.config.ts`
- Modify: `moorestech_web/webui/tsconfig.json`（`paths` 追加）
- Modify: `moorestech_web/webui/vite.config.ts`（`resolve.alias` 追加）

- [ ] **Step 1: 依存を追加**

Run（`moorestech_web/webui` で）:
```bash
pnpm add zustand
pnpm add -D vitest @vitejs/plugin-react @playwright/test ws @types/ws tsx
pnpm exec playwright install chromium
```
Expected: `package.json` に `zustand`(deps) と `vitest`/`@playwright/test`/`ws`/`@types/ws`/`tsx`(devDeps) が入る。

- [ ] **Step 2: scripts を追加**

`package.json` の `scripts` を以下に置換:
```json
  "scripts": {
    "dev": "vite",
    "build": "tsc -b && vite build",
    "preview": "vite preview",
    "test": "vitest run",
    "test:watch": "vitest",
    "test:e2e": "tsc -p e2e/tsconfig.json --noEmit && playwright test"
  },
```

- [ ] **Step 3: `@/` パスエイリアスを tsconfig に追加**

`tsconfig.json` の `compilerOptions` に追記:
```json
    "baseUrl": ".",
    "paths": { "@/*": ["src/*"] },
```

- [ ] **Step 4: vite にも alias を追加（tsconfig paths は vite が自動解決しないため）**

`vite.config.ts` 冒頭に `import { fileURLToPath, URL } from "node:url";` を追加し、`defineConfig({` 直下に:
```ts
  resolve: {
    alias: { "@": fileURLToPath(new URL("./src", import.meta.url)) },
  },
```

- [ ] **Step 5: vitest.config.ts を作成**

```ts
import { defineConfig } from "vitest/config";
import { fileURLToPath, URL } from "node:url";

// 繊細な純粋ロジックの単体テスト。DOM 不要のため node 環境
// Unit tests for pure logic; node env since no DOM is needed
export default defineConfig({
  resolve: {
    alias: { "@": fileURLToPath(new URL("./src", import.meta.url)) },
  },
  test: {
    environment: "node",
    include: ["src/**/*.test.ts"],
  },
});
```

- [ ] **Step 6: 既存ビルドが壊れていないか確認**

Run: `pnpm build`
Expected: 既存コードのまま PASS（型変更なし、alias 追加のみ）。

- [ ] **Step 7: Commit**

```bash
git add moorestech_web/webui/package.json moorestech_web/webui/pnpm-lock.yaml moorestech_web/webui/tsconfig.json moorestech_web/webui/vite.config.ts moorestech_web/webui/vitest.config.ts
git commit -m "chore(webui): zustand/vitest/playwright と @/ alias を導入"
```

---

## Task 1: 型レイヤ（payloadTypes / protocol / 型付き hooks / safeParse）

サーバー由来DTOを `bridge/payloadTypes.ts` に集約、Topic/Action 契約を `bridge/protocol.ts` に集約し、`useTopic`/`subscribeTopic`/`dispatchAction` を型付けする。**この段では呼び出し置換は最小限**（既存 `../types/*` import は Task 7 のフォルダ移動でまとめて整理。ここでは新規型ファイル追加と hooks のジェネリク制約のみ）。

**Files:**
- Create: `src/bridge/payloadTypes.ts`
- Create: `src/bridge/protocol.ts`
- Modify: `src/bridge/webSocketClient.ts`（`subscribeTopic` 型制約 + `safeParse` ガード）
- Modify: `src/bridge/useTopic.ts`（型制約）
- Modify: `src/bridge/actions.ts`（型制約）

- [ ] **Step 1: payloadTypes.ts を作成（既存 types/* の内容を集約・再export）**

```ts
// サーバー由来の DTO 型（topic snapshot / event の data 部）
// Server-originated DTO types (the data part of topic snapshot/event)
// 現状は手書き。将来 C# からの自動生成に置換する余地を残す
// Handwritten for now; leaves room to be replaced by C#-generated types later

export type SlotData = { itemId: number; count: number };
export type InventoryArea = "main" | "hotbar" | "grab";
export type SlotRef = { area: InventoryArea; slot: number };

export type PlayerInventoryData = {
  mainSlots: SlotData[];
  hotbarSlots: SlotData[];
  grab: SlotData;
};

export type RequiredItem = { itemId: number; count: number };
export type CraftRecipe = {
  recipeGuid: string;
  resultItemId: number;
  resultCount: number;
  craftTime: number;
  requiredItems: RequiredItem[];
};
export type CraftRecipesData = { recipes: CraftRecipe[] };

export type MachineRecipeItem = { itemId: number; count: number };
export type MachineRecipe = {
  recipeGuid: string;
  blockItemId: number;
  blockName: string;
  time: number;
  inputItems: MachineRecipeItem[];
  outputItems: MachineRecipeItem[];
};
export type MachineRecipesData = { recipes: MachineRecipe[] };

export type RecipeViewerItemListData = { itemIds: number[] };

export type ItemMasterEntry = { itemId: number; name: string; maxStack: number };
export type ItemMasterData = { items: ItemMasterEntry[] };
```

- [ ] **Step 2: protocol.ts を作成（Topics / TopicPayloads / ActionPayloads / メッセージ型）**

```ts
import type {
  PlayerInventoryData,
  CraftRecipesData,
  MachineRecipesData,
  RecipeViewerItemListData,
  SlotRef,
} from "./payloadTypes";

// 通信の op レベルのメッセージ型（webSocketClient が使用）
// Wire-level message types at the op layer (used by webSocketClient)
export type ServerMsg =
  | { op: "snapshot"; topic: string; data: unknown }
  | { op: "event"; topic: string; data: unknown }
  | { op: "result"; requestId: string; ok: boolean; error?: string };

export type ClientMsg =
  | { op: "subscribe"; topics: string[] }
  | { op: "unsubscribe"; topics: string[] }
  | { op: "snapshot"; topic: string }
  | { op: "action"; type: string; requestId: string; payload: unknown };

export type ActionResult = { ok: boolean; error?: string };

// topic 名の単一の真実。文字列リテラルの散在を防ぐ
// Single source of truth for topic names; prevents scattered string literals
export const Topics = {
  inventory: "local_player.inventory",
  craftRecipes: "crafting.recipes",
  machineRecipes: "crafting.machine_recipes",
  itemList: "recipe_viewer.item_list",
} as const;

// topic → payload 型の対応表。useTopic/subscribeTopic がこれで型付けされる
// topic → payload type registry; types useTopic/subscribeTopic
export type TopicPayloads = {
  [Topics.inventory]: PlayerInventoryData;
  [Topics.craftRecipes]: CraftRecipesData;
  [Topics.machineRecipes]: MachineRecipesData;
  [Topics.itemList]: RecipeViewerItemListData;
};

// action type → payload 型の対応表。dispatchAction がこれで型付けされる
// action type → payload type registry; types dispatchAction
export type ActionPayloads = {
  "inventory.move_item": { from: SlotRef; to: SlotRef; count: number };
  "inventory.split": { from: SlotRef };
  "inventory.collect": { target: SlotRef };
  "inventory.sort": Record<string, never>;
  "craft.execute": { recipeGuid: string };
  "debug.echo": { hello: string };
};
```

> 注: `inventory.collect` の payload は**フェーズAでは現状の `{ target }` を維持**する。`{ slot }` への変更はフェーズB。

- [ ] **Step 3: webSocketClient.ts に safeParse ガードと型付き subscribeTopic を入れる**

ファイル冒頭の手書き `ServerMsg`/`ClientMsg`/`ActionResult` 定義を削除し、protocol から import する。先頭付近を:
```ts
// Unity 側 Web UI ホストと通信する WebSocket クライアント
// WebSocket client for the Unity-side Web UI host
import type { ServerMsg, ClientMsg, ActionResult, TopicPayloads } from "./protocol";

export type { ActionResult };

// 壊れたフレームで handler が落ちるのを防ぐ JSON.parse ラッパ。
// AGENTS規約「try-catch原則禁止」の正当な例外として、try-catch をここに隔離し呼び出し側は null 分岐
// Guarded JSON.parse so a broken frame can't crash the handler.
// Justified exception to the no-try-catch rule: try-catch is isolated here; callers branch on null
function safeParse(raw: string): Partial<ServerMsg> | null {
  try {
    return JSON.parse(raw) as Partial<ServerMsg>;
  } catch {
    return null;
  }
}
```
`ws.onmessage` の中の `const msg = JSON.parse(ev.data) as Partial<ServerMsg>;` を:
```ts
      const msg = safeParse(ev.data);
      if (!msg) return;
```
に置換。

ファイル末尾の `subscribeTopic` を型制約版に置換:
```ts
export function subscribeTopic<K extends keyof TopicPayloads>(
  topic: K,
  listener: (data: TopicPayloads[K]) => void,
) {
  // as TopicPayloads[K] はランタイム非保証のキャスト境界。未知 topic は購読されないため到達しない
  // This cast is a runtime-unchecked boundary; unknown topics are never subscribed so they don't reach here
  return client.subscribe(topic, (d) => listener(d as TopicPayloads[K]));
}
```

- [ ] **Step 4: useTopic.ts を型付け**

```ts
import { useEffect, useState } from "react";
import { subscribeTopic } from "./webSocketClient";
import type { TopicPayloads } from "./protocol";

// 指定トピックを購読して最新の値を返す React フック（初回 snapshot 前は null）
// React hook that subscribes to a topic and returns the latest value (null before the first snapshot)
export function useTopic<K extends keyof TopicPayloads>(topic: K): TopicPayloads[K] | null {
  const [value, setValue] = useState<TopicPayloads[K] | null>(null);
  useEffect(() => {
    const unsub = subscribeTopic(topic, (data) => setValue(data));
    return unsub;
  }, [topic]);
  return value;
}
```

- [ ] **Step 5: actions.ts を型付け**

```ts
import { sendAction } from "./webSocketClient";
import { showToast } from "./toastBus";
import type { ActionPayloads } from "./protocol";

// action を発行し、失敗時はトースト表示して false を返す UI 向けラッパ
// UI-facing wrapper: dispatch an action, toast on failure, return success flag
// true は「サーバーが受理した」ことを意味し、topic event の反映完了を保証しない
// true means the server accepted the action; it does not guarantee the topic event has arrived yet
export async function dispatchAction<K extends keyof ActionPayloads>(
  type: K,
  payload: ActionPayloads[K],
): Promise<boolean> {
  try {
    const result = await sendAction(type, payload);
    if (!result.ok) {
      showToast(`${type} failed: ${result.error ?? "unknown"}`);
      return false;
    }
    return true;
  } catch (e) {
    showToast(`${type} error: ${e instanceof Error ? e.message : String(e)}`);
    return false;
  }
}
```

> `dispatchAction` の try-catch は **既存実装の維持**（reject 処理が本質）であり AGENTS の禁止対象外（Promise reject のハンドリングは条件分岐で代替不可）。既存コメントのまま温存。

- [ ] **Step 6: 呼び出し側の topic 文字列を Topics 定数へ最小限差し替え**

`useTopic("local_player.inventory")` 等の string リテラルは Task 7 で feature 移動と同時に `Topics.inventory` へ置換する。**このタスクでは既存の string リテラル呼び出しがそのまま型エラーにならないことだけ確認**（`useTopic<K extends keyof TopicPayloads>` は string リテラル `"local_player.inventory"` を受け付ける）。型が通ることを次stepで確認。

- [ ] **Step 7: ビルドで型を確認**

Run: `pnpm build`
Expected: PASS。既存コンポーネントの `useTopic<PlayerInventoryData>("local_player.inventory")` は **明示ジェネリク指定が新シグネチャと衝突する**ため、衝突したら呼び出し側を `useTopic("local_player.inventory")`（推論）へ修正する。対象: `InventoryPanel.tsx`/`RecipeViewer.tsx`/`ItemListPanel.tsx` の `useTopic<...>(...)` を全て明示ジェネリク無しに変更。

- [ ] **Step 8: 再ビルド確認 → Commit**

Run: `pnpm build` → Expected: PASS
```bash
git add moorestech_web/webui/src/bridge
git commit -m "feat(webui): protocol/payloadTypes で topic/action を型付け + JSON.parse をsafeParse隔離"
```

---

## Task 2: Playwright モックWSホスト + e2e（挙動セーフティネット）

リファクタ前に現状のユーザーフローを固定する。モックホストは `bridge/protocol` 型を import し、`result`(ack) と `event`(topic push) を別経路・遅延付きで再現する。

**Files:**
- Create: `e2e/mock-host/server.ts`
- Create: `e2e/mock-host/fixtures.ts`
- Create: `e2e/tsconfig.json`
- Create: `e2e/playwright.config.ts`
- Create: `e2e/tests/inventory.spec.ts`
- Create: `e2e/tests/recipe.spec.ts`
- Create: `e2e/tests/toast.spec.ts`
- Modify: `package.json`（`test:e2e` は Task 0 で追加済み。`playwright.config.ts` の場所を `e2e/` に合わせ `--config` 指定）

> `test:e2e` を `tsc -p e2e/tsconfig.json --noEmit && playwright test --config e2e/playwright.config.ts` に更新する。

- [ ] **Step 1: e2e/tsconfig.json（mock-host と spec を src/bridge 型で型検査）**

```json
{
  "compilerOptions": {
    "target": "ES2022",
    "module": "ESNext",
    "moduleResolution": "Bundler",
    "strict": true,
    "esModuleInterop": true,
    "skipLibCheck": true,
    "types": ["node"],
    "noEmit": true,
    "baseUrl": "..",
    "paths": { "@/*": ["src/*"] }
  },
  "include": ["**/*.ts", "../src/bridge/protocol.ts", "../src/bridge/payloadTypes.ts"]
}
```

- [ ] **Step 2: fixtures.ts（protocol 型に satisfies する canned データ）**

```ts
import type {
  PlayerInventoryData,
  CraftRecipesData,
  MachineRecipesData,
  RecipeViewerItemListData,
  ItemMasterData,
} from "../../src/bridge/payloadTypes";

const empty = { itemId: 0, count: 0 };

// 9列×4行のメイン + 9列ホットバー。先頭にクラフト素材を仕込む
// 9x4 main + 9 hotbar; seed craft materials at the front
export const inventory = {
  mainSlots: [
    { itemId: 1, count: 10 },
    { itemId: 2, count: 10 },
    ...Array.from({ length: 34 }, () => ({ ...empty })),
  ],
  hotbarSlots: Array.from({ length: 9 }, () => ({ ...empty })),
  grab: { ...empty },
} satisfies PlayerInventoryData;

// collect 実行後の snapshot（同種を1スロットへ集約した想定の canned 結果）
// Post-collect snapshot (canned: same items consolidated into one slot)
export const inventoryAfterCollect = {
  mainSlots: [
    { itemId: 1, count: 20 },
    ...Array.from({ length: 35 }, () => ({ ...empty })),
  ],
  hotbarSlots: Array.from({ length: 9 }, () => ({ ...empty })),
  grab: { ...empty },
} satisfies PlayerInventoryData;

export const craftRecipes = {
  recipes: [
    {
      recipeGuid: "g-craft-1",
      resultItemId: 100,
      resultCount: 1,
      craftTime: 1,
      requiredItems: [
        { itemId: 1, count: 2 },
        { itemId: 2, count: 1 },
      ],
    },
  ],
} satisfies CraftRecipesData;

export const machineRecipes = { recipes: [] } satisfies MachineRecipesData;

export const itemList = { itemIds: [100, 1, 2] } satisfies RecipeViewerItemListData;

export const itemMaster = {
  items: [
    { itemId: 1, name: "Wood", maxStack: 100 },
    { itemId: 2, name: "Stone", maxStack: 100 },
    { itemId: 100, name: "Plank", maxStack: 100 },
  ],
} satisfies ItemMasterData;
```

- [ ] **Step 3: server.ts（http で dist 配信 + /api + /ws、result と event を別経路で遅延）**

```ts
import { createServer } from "node:http";
import { readFile } from "node:fs/promises";
import { extname, join, normalize } from "node:path";
import { fileURLToPath } from "node:url";
import { WebSocketServer, type WebSocket } from "ws";
import type { ClientMsg } from "../../src/bridge/protocol";
import { Topics } from "../../src/bridge/protocol";
import * as fx from "./fixtures";

const DIST = fileURLToPath(new URL("../../dist", import.meta.url));
const PORT = Number(process.env.MOCK_PORT ?? 5273);

// 受信 action を記録（送信契約 assert 用に /__actions で返す）
// Record received actions (exposed at /__actions to assert the send contract)
const received: { type: string; payload: unknown }[] = [];

const MIME: Record<string, string> = {
  ".html": "text/html",
  ".js": "text/javascript",
  ".css": "text/css",
  ".json": "application/json",
};

const server = createServer(async (req, res) => {
  const url = req.url ?? "/";
  if (url === "/api/master/items") {
    res.setHeader("content-type", "application/json");
    res.end(JSON.stringify(fx.itemMaster));
    return;
  }
  if (url === "/__actions") {
    res.setHeader("content-type", "application/json");
    res.end(JSON.stringify(received));
    return;
  }
  if (url.startsWith("/api/icons/")) {
    // アイコンは 404 にして UI の #id フォールバックに任せる
    // Return 404 for icons; the UI falls back to the #id label
    res.statusCode = 404;
    res.end();
    return;
  }
  // 静的配信（SPA なので未知パスは index.html）
  // Static serving; unknown paths fall back to index.html (SPA)
  const rel = url === "/" ? "/index.html" : url.split("?")[0];
  const path = normalize(join(DIST, rel));
  const data = await readFile(path).catch(() => null);
  if (data === null) {
    const html = await readFile(join(DIST, "index.html"));
    res.setHeader("content-type", "text/html");
    res.end(html);
    return;
  }
  res.setHeader("content-type", MIME[extname(path)] ?? "application/octet-stream");
  res.end(data);
});

const wss = new WebSocketServer({ server, path: "/ws" });

// topic → 現在 push する snapshot。collect 後は差し替える
// topic → snapshot currently pushed; swapped after collect
const topicData: Record<string, unknown> = {
  [Topics.inventory]: fx.inventory,
  [Topics.craftRecipes]: fx.craftRecipes,
  [Topics.machineRecipes]: fx.machineRecipes,
  [Topics.itemList]: fx.itemList,
};

function send(ws: WebSocket, obj: unknown) {
  ws.send(JSON.stringify(obj));
}

wss.on("connection", (ws) => {
  ws.on("message", (raw) => {
    const msg = JSON.parse(raw.toString()) as ClientMsg;
    if (msg.op === "subscribe") {
      for (const topic of msg.topics) {
        if (topic in topicData) send(ws, { op: "snapshot", topic, data: topicData[topic] });
      }
      return;
    }
    if (msg.op === "action") {
      received.push({ type: msg.type, payload: msg.payload });
      // result(ack) は即時、topic event は数十ms 後に別経路で push（stale grab 再現）
      // ack is immediate; the topic event is pushed later on a separate channel (reproduces stale grab)
      const ok = msg.type !== "fail.always";
      send(ws, { op: "result", requestId: msg.requestId, ok, error: ok ? undefined : "mock_error" });
      if (msg.type === "inventory.collect") {
        topicData[Topics.inventory] = fx.inventoryAfterCollect;
        setTimeout(() => send(ws, { op: "event", topic: Topics.inventory, data: fx.inventoryAfterCollect }), 30);
      }
      return;
    }
  });
});

server.listen(PORT, () => console.log(`mock-host on ${PORT}`));
```

- [ ] **Step 4: playwright.config.ts（build→mock host を webServer 起動）**

`e2e/playwright.config.ts`:
```ts
import { defineConfig } from "@playwright/test";

const PORT = 5273;

export default defineConfig({
  testDir: "./tests",
  timeout: 15_000,
  use: { baseURL: `http://127.0.0.1:${PORT}` },
  webServer: {
    command: "pnpm build && pnpm tsx e2e/mock-host/server.ts",
    cwd: "..",
    port: PORT,
    reuseExistingServer: false,
    timeout: 120_000,
    env: { MOCK_PORT: String(PORT) },
  },
});
```
> `package.json` の `test:e2e` を `tsc -p e2e/tsconfig.json --noEmit && playwright test --config e2e/playwright.config.ts` に更新。

- [ ] **Step 5: inventory.spec.ts（接続→描画、左クリックでgrab追従、split送信契約）**

```ts
import { test, expect } from "@playwright/test";

test("接続後にインベントリが描画される", async ({ page }) => {
  await page.goto("/");
  await expect(page.getByRole("heading", { name: "Inventory" })).toBeVisible();
  // Wood(itemId=1,count=10) の count バッジが出る
  await expect(page.getByText("10").first()).toBeVisible();
});

test("左クリックで grab オーバーレイが追従する", async ({ page }) => {
  await page.goto("/");
  await expect(page.getByRole("heading", { name: "Inventory" })).toBeVisible();
  const firstSlot = page.locator(".grid.grid-cols-9 > div").first();
  await firstSlot.click();
  // grab オーバーレイは fixed z-40。出現を確認
  await expect(page.locator(".fixed.z-40")).toBeVisible();
});

test("右クリックで inventory.split を送る", async ({ page }) => {
  await page.goto("/");
  await expect(page.getByRole("heading", { name: "Inventory" })).toBeVisible();
  const firstSlot = page.locator(".grid.grid-cols-9 > div").first();
  await firstSlot.click({ button: "right" });
  await expect.poll(async () => {
    const actions = await page.request.get("/__actions").then((r) => r.json());
    return actions.some((a: { type: string }) => a.type === "inventory.split");
  }).toBe(true);
});
```

- [ ] **Step 6: recipe.spec.ts（アイテム選択→craft enable→送信、pager/tab リセット）**

```ts
import { test, expect } from "@playwright/test";

test("アイテム選択でレシピ表示、craft 可能なら送信できる", async ({ page }) => {
  await page.goto("/");
  await expect(page.getByRole("heading", { name: "Items" })).toBeVisible();
  // 右リストの Plank(100) を選択
  await page.getByRole("heading", { name: "Items" })
    .locator("..")
    .locator(".grid > div").first().click();
  await expect(page.getByRole("button", { name: "Craft" })).toBeEnabled();
  await page.getByRole("button", { name: "Craft" }).click();
  await expect.poll(async () => {
    const actions = await page.request.get("/__actions").then((r) => r.json());
    return actions.some((a: { type: string }) => a.type === "craft.execute");
  }).toBe(true);
});
```

- [ ] **Step 7: toast.spec.ts（失敗 action でトースト）**

```ts
import { test, expect } from "@playwright/test";

test("Ping Action ボタンは成功トーストを出す", async ({ page }) => {
  await page.goto("/");
  await page.getByRole("button", { name: "Ping Action" }).click();
  await expect(page.getByText("debug.echo ok")).toBeVisible();
});
```

- [ ] **Step 8: e2e を実行して緑を確認**

Run: `pnpm test:e2e`
Expected: tsc PASS、Playwright 全 spec PASS。
（失敗時は selector を現状 DOM に合わせて調整。grab overlay クラス・grid 構造は現行 `InventoryPanel.tsx` 準拠。）

- [ ] **Step 9: Commit**

```bash
git add moorestech_web/webui/e2e moorestech_web/webui/package.json
git commit -m "test(webui): Playwright モックWSホストで現状フローを固定(セーフティネット)"
```

---

## Task 3: 繊細ロジックを純粋関数化 + vitest

副作用（`clickGrabHistory` 更新・`dispatchAction`）はコンポーネントに残し、判定だけ抽出する。

**Files:**
- Create: `src/features/inventory/inventoryLogic.ts`
- Create: `src/features/inventory/inventoryLogic.test.ts`
- Create: `src/features/recipe/craftLogic.ts`
- Create: `src/features/recipe/craftLogic.test.ts`

> このタスクでは `features/` ディレクトリを新設して **ロジックのみ**置く。コンポーネント本体の移動は Task 7。

- [ ] **Step 1: inventoryLogic.ts の失敗テストを書く**

`src/features/inventory/inventoryLogic.test.ts`:
```ts
import { describe, it, expect } from "vitest";
import { resolveDirectMoveTarget } from "./inventoryLogic";
import type { SlotData } from "@/bridge/payloadTypes";

const slots = (xs: SlotData[]): SlotData[] => xs;

describe("resolveDirectMoveTarget", () => {
  it("同種スタックが空きありなら優先する", () => {
    const target = slots([
      { itemId: 5, count: 1 },
      { itemId: 0, count: 0 },
    ]);
    expect(resolveDirectMoveTarget(target, 5, 100)).toBe(0);
  });

  it("同種スタックが満杯なら空スロットへ", () => {
    const target = slots([
      { itemId: 5, count: 100 },
      { itemId: 0, count: 0 },
    ]);
    expect(resolveDirectMoveTarget(target, 5, 100)).toBe(1);
  });

  it("maxStack 未指定(undefined)なら同種探索を飛ばし空スロットへ", () => {
    const target = slots([
      { itemId: 5, count: 1 },
      { itemId: 0, count: 0 },
    ]);
    expect(resolveDirectMoveTarget(target, 5, undefined)).toBe(1);
  });

  it("移動先が無ければ -1", () => {
    const target = slots([{ itemId: 9, count: 100 }]);
    expect(resolveDirectMoveTarget(target, 5, 100)).toBe(-1);
  });
});
```

- [ ] **Step 2: テストが落ちることを確認**

Run: `pnpm test`
Expected: FAIL（`resolveDirectMoveTarget` 未定義）。

- [ ] **Step 3: inventoryLogic.ts を実装（現 directMove のロジックを正確に移植）**

```ts
import type { SlotData } from "@/bridge/payloadTypes";

// Shift+クリックの移動先 index を決める。同種スタック(空きあり)優先→空スロット→無ければ -1。
// maxStack が undefined（マスタ未ロード）なら同種探索を飛ばし空スロットのみ。
// Decide the direct-move target index: same-item stack with room first, then empty, else -1.
// When maxStack is undefined (master unloaded) skip the same-item search and use empty only.
export function resolveDirectMoveTarget(
  targetSlots: SlotData[],
  itemId: number,
  maxStack: number | undefined,
): number {
  const stackable =
    maxStack === undefined ? -1 : targetSlots.findIndex((s) => s.itemId === itemId && s.count < maxStack);
  const empty = targetSlots.findIndex((s) => s.count === 0);
  return stackable >= 0 ? stackable : empty;
}
```

- [ ] **Step 4: テスト緑を確認**

Run: `pnpm test`
Expected: `resolveDirectMoveTarget` の4ケース PASS。

- [ ] **Step 5: craftLogic.ts の失敗テストを書く**

`src/features/recipe/craftLogic.test.ts`:
```ts
import { describe, it, expect } from "vitest";
import { buildOwnedCounts, craftable, clampIndex } from "./craftLogic";
import type { PlayerInventoryData, CraftRecipe } from "@/bridge/payloadTypes";

const inv = (main: [number, number][], hot: [number, number][], grab: [number, number]): PlayerInventoryData => ({
  mainSlots: main.map(([itemId, count]) => ({ itemId, count })),
  hotbarSlots: hot.map(([itemId, count]) => ({ itemId, count })),
  grab: { itemId: grab[0], count: grab[1] },
});

describe("buildOwnedCounts", () => {
  it("main+hotbar を合算し grab を除外する", () => {
    const counts = buildOwnedCounts(inv([[1, 3]], [[1, 2]], [1, 99]));
    expect(counts.get(1)).toBe(5);
  });
  it("count 0 は加算しない", () => {
    const counts = buildOwnedCounts(inv([[0, 0]], [[2, 4]], [0, 0]));
    expect(counts.get(0)).toBeUndefined();
    expect(counts.get(2)).toBe(4);
  });
});

describe("craftable", () => {
  const recipe = {
    recipeGuid: "g", resultItemId: 9, resultCount: 1, craftTime: 1,
    requiredItems: [{ itemId: 1, count: 2 }, { itemId: 2, count: 1 }],
  } satisfies CraftRecipe;
  it("全素材を満たせば true", () => {
    expect(craftable(recipe, new Map([[1, 2], [2, 1]]))).toBe(true);
  });
  it("一つでも不足なら false", () => {
    expect(craftable(recipe, new Map([[1, 1], [2, 1]]))).toBe(false);
  });
});

describe("clampIndex", () => {
  it("length 内に収める", () => {
    expect(clampIndex(5, 3)).toBe(2);
  });
  it("0 未満にしない", () => {
    expect(clampIndex(0, 0)).toBe(0);
  });
});
```

- [ ] **Step 6: テストが落ちることを確認**

Run: `pnpm test`
Expected: FAIL（`craftLogic` 未定義）。

- [ ] **Step 7: craftLogic.ts を実装**

```ts
import type { PlayerInventoryData, CraftRecipe } from "@/bridge/payloadTypes";

// 所持数集計。サーバーの OneClickCraft が main+hotbar のみ見るため grab は除外。
// Owned-count tally; grab is excluded because the server's OneClickCraft only consults main+hotbar.
export function buildOwnedCounts(inventory: PlayerInventoryData): Map<number, number> {
  const counts = new Map<number, number>();
  const add = (id: number, count: number) => {
    if (count > 0) counts.set(id, (counts.get(id) ?? 0) + count);
  };
  inventory.mainSlots.forEach((s) => add(s.itemId, s.count));
  inventory.hotbarSlots.forEach((s) => add(s.itemId, s.count));
  return counts;
}

// 全必要素材を所持数が満たすか。
// Whether owned counts satisfy every required material.
export function craftable(recipe: CraftRecipe, counts: Map<number, number>): boolean {
  return recipe.requiredItems.every((r) => (counts.get(r.itemId) ?? 0) >= r.count);
}

// recipeIndex を [0, length-1] にクランプ。呼び出し側が length>0 を保証する契約。
// Clamp recipeIndex into [0, length-1]; caller must guarantee length>0.
export function clampIndex(index: number, length: number): number {
  return Math.max(0, Math.min(index, length - 1));
}
```

- [ ] **Step 8: テスト緑を確認 → Commit**

Run: `pnpm test`
Expected: 全 PASS。
```bash
git add moorestech_web/webui/src/features
git commit -m "test(webui): directMove/craft 判定を純関数化し vitest で固定"
```

---

## Task 4: 純関数をコンポーネントへ配線（挙動不変）

**Files:**
- Modify: `src/components/InventoryPanel.tsx`（`directMove` を `resolveDirectMoveTarget` 使用に）
- Modify: `src/components/RecipeViewer.tsx`（counts を `buildOwnedCounts` に）
- Modify: `src/components/CraftRecipeView.tsx`（clamp/craftable を craftLogic に）
- Modify: `src/components/MachineRecipeView.tsx`（clamp を craftLogic に）

- [ ] **Step 1: InventoryPanel.directMove を置換**

`directMove` 内の `stackable`/`empty`/`target` 算出3行を:
```ts
    const maxStack = itemMaster?.get(slot.itemId)?.maxStack;
    const target = resolveDirectMoveTarget(targetSlots, slot.itemId, maxStack);
    if (target < 0) return;
```
に置換し、冒頭 import に `import { resolveDirectMoveTarget } from "../features/inventory/inventoryLogic";` を追加。

- [ ] **Step 2: RecipeViewer の counts 集計を置換**

`RecipeContent` 内の `const counts = new Map...` から `hotbarSlots.forEach(...)` までを:
```ts
  const counts = buildOwnedCounts(inventory);
```
に置換し、import に `import { buildOwnedCounts } from "./craftLogic";`（Task 7 前は相対 `../features/recipe/craftLogic`）を追加。

> 注: Task 7 前のためここでは `import { buildOwnedCounts } from "../features/recipe/craftLogic";`。

- [ ] **Step 3: CraftRecipeView の index/craftable を置換**

```ts
  const index = clampIndex(recipeIndex, recipes.length);
  const recipe = recipes[index];
  const isCraftable = craftable(recipe, counts);
```
（変数名 `craftable` は関数名と衝突するので `isCraftable` にリネームし、JSX の `craftable ?`/`!craftable` を `isCraftable`/`!isCraftable` に更新。）
import 追加: `import { clampIndex, craftable } from "../features/recipe/craftLogic";`

- [ ] **Step 4: MachineRecipeView の index を置換**

```ts
  const index = clampIndex(recipeIndex, recipes.length);
```
import 追加: `import { clampIndex } from "../features/recipe/craftLogic";`

- [ ] **Step 5: ビルド + e2e 回帰で挙動不変を確認**

Run: `pnpm build && pnpm test && pnpm test:e2e`
Expected: 全 PASS（e2e セーフティネットが挙動不変を保証）。

- [ ] **Step 6: Commit**

```bash
git add moorestech_web/webui/src/components
git commit -m "refactor(webui): 純関数化したロジックをコンポーネントへ配線(挙動不変)"
```

---

## Task 5: Zustand 化（selectedItemId + toast のみ）

**Files:**
- Create: `src/app/uiStore.ts`
- Create: `src/features/toast/toastStore.ts`
- Modify: `src/bridge/toastBus.ts`（toastStore へ委譲、または置換）
- Modify: `src/App.tsx`（props バケツリレー廃止）
- Modify: `src/components/RecipeViewer.tsx`（`itemId`/`onSelect` props → store）
- Modify: `src/components/ItemListPanel.tsx`（props → store）
- Modify: `src/components/ToastHost.tsx`（store 購読）

- [ ] **Step 1: uiStore.ts を作成**

```ts
import { create } from "zustand";

// UI ローカル状態のみ（remote topic データは入れない）。grab は WS topic 由来なので絶対に入れない。
// UI-local state only (no remote topic data here). grab comes from a WS topic and must never live here.
type UiState = {
  selectedItemId: number | null;
  setSelectedItem: (itemId: number) => void;
};

export const useUiStore = create<UiState>((set) => ({
  selectedItemId: null,
  setSelectedItem: (itemId) => set({ selectedItemId: itemId }),
}));
```

- [ ] **Step 2: toastStore.ts を作成（React 外 emit を保全）**

```ts
import { create } from "zustand";

export type Toast = { id: number; message: string };

type ToastState = {
  toasts: Toast[];
  addToast: (message: string) => void;
  removeToast: (id: number) => void;
};

let nextId = 1;

export const useToastStore = create<ToastState>((set) => ({
  toasts: [],
  addToast: (message) => {
    const id = nextId++;
    set((s) => ({ toasts: [...s.toasts, { id, message }] }));
    // 3秒で自動消滅（既存 ToastHost の挙動を保全）
    // Auto-dismiss after 3s (preserves the existing ToastHost behavior)
    setTimeout(() => set((s) => ({ toasts: s.toasts.filter((t) => t.id !== id) })), 3000);
  },
  removeToast: (id) => set((s) => ({ toasts: s.toasts.filter((t) => t.id !== id) })),
}));

// React 外（bridge/actions.ts）からの emit を getState 経由で保全
// Preserve emits from outside React (bridge/actions.ts) via getState
export function emitToast(message: string) {
  useToastStore.getState().addToast(message);
}
```

- [ ] **Step 3: toastBus.ts を emitToast へ委譲に変更**

`bridge/toastBus.ts` の `showToast` を toastStore 委譲へ:
```ts
// トースト通知。React 外（bridge層）からも emit できる。実体は toast feature の store。
// Toast notifications, emittable from outside React (bridge layer). Backed by the toast feature store.
import { emitToast } from "../features/toast/toastStore";

export function showToast(message: string) {
  emitToast(message);
}
```
> `subscribeToast` は不要になるため削除（ToastHost が store 購読に変わる）。**依存方向注意**: `bridge` が `features` を import するのは契約違反。→ **回避**: `toastBus.ts` を Task 7 で `features/toast` 配下へ移すか、`actions.ts` から直接 `emitToast` を呼ぶ。本 step では暫定的に `actions.ts` の `showToast` 呼び出しを `emitToast` 直呼びに変更し、`toastBus.ts` を削除する方針を採る（下 step 4）。

- [ ] **Step 4: toastBus.ts を廃止し actions.ts を emitToast 直呼びに**

`bridge/toastBus.ts` を削除。`bridge/actions.ts` の import を `import { showToast } from "./toastBus";` → `import { emitToast } from "../features/toast/toastStore";` に変更し、本文の `showToast(...)` を `emitToast(...)` に置換。
`components/DebugActionButton.tsx` の `import { showToast } from "../bridge/toastBus";` → `import { emitToast } from "../features/toast/toastStore";`、`showToast(...)` → `emitToast(...)`。

> 依存方向: `bridge/actions.ts` → `features/toast` は **契約違反（bridge→features）**。これを避けるため、**`dispatchAction` を `features` 側へは移さず**、toast 通知の仕組みだけ bridge に残す設計に倒す。具体的には:
> - `features/toast/toastStore.ts` はそのまま。
> - bridge には `bridge/notify.ts` を新設し、`let sink: (m: string) => void = () => {};` と `export function setToastSink(fn)` / `export function notify(m)` を置く。
> - `actions.ts` は `bridge/notify.ts` の `notify` を呼ぶ（bridge 内で完結）。
> - `features/toast` 側で起動時に `setToastSink(emitToast)` を1回登録（`ToastHost` マウント時 or `app/main.tsx`）。
>
> **この notify sink 方式を採用する**（bridge→features の逆流を作らない）。下 step で確定実装。

- [ ] **Step 5: bridge/notify.ts を新設（sink 方式で逆依存を断つ）**

`src/bridge/notify.ts`:
```ts
// bridge から UI への一方向通知 sink。bridge は features を import しないため、
// 実体（toast store）は起動時に features 側から注入する。
// One-way notify sink from bridge to UI. bridge never imports features,
// so the concrete sink (toast store) is injected from the feature side at startup.
let sink: (message: string) => void = () => {};

export function setToastSink(fn: (message: string) => void) {
  sink = fn;
}

export function notify(message: string) {
  sink(message);
}
```
`bridge/actions.ts` を `import { notify } from "./notify";` にし、`showToast`/`emitToast` 呼び出しを `notify(...)` に置換。`bridge/toastBus.ts` は削除。

- [ ] **Step 6: 起動時に sink を登録**

`src/main.tsx` に追加（`createRoot` の前）:
```ts
import { setToastSink } from "./bridge/notify";
import { emitToast } from "./features/toast/toastStore";

setToastSink(emitToast);
```
`DebugActionButton.tsx` は `import { emitToast } from "../features/toast/toastStore";` で `emitToast("debug.echo ok")` を直呼び（feature→feature は問題なし。DebugActionButton は Task 7 で app/ 配下へ）。

- [ ] **Step 7: ToastHost を store 購読に**

`components/ToastHost.tsx`:
```ts
import { useToastStore } from "../features/toast/toastStore";

// 画面右下にトーストを表示するホスト。自動消滅は store 側（addToast）で管理
// Toast host pinned to the bottom-right; auto-dismiss is handled in the store (addToast)
export default function ToastHost() {
  const toasts = useToastStore((s) => s.toasts);
  return (
    <div className="fixed bottom-4 right-4 space-y-2 z-50">
      {toasts.map((t) => (
        <div key={t.id} className="bg-red-800 text-white text-sm rounded px-3 py-2 shadow">
          {t.message}
        </div>
      ))}
    </div>
  );
}
```

- [ ] **Step 8: App / RecipeViewer / ItemListPanel を store 化**

`App.tsx`: `useState<selectedItemId>` を廃止し props を渡さない:
```tsx
import InventoryPanel from "./components/InventoryPanel";
import RecipeViewer from "./components/RecipeViewer";
import ItemListPanel from "./components/ItemListPanel";
import ToastHost from "./components/ToastHost";
import DebugActionButton from "./components/DebugActionButton";

export default function App() {
  return (
    <div className="p-4 min-h-screen grid gap-6" style={{ /* 既存 gridTemplate* をそのまま */ }}>
      <div className="flex items-center gap-4 [grid-area:header]">
        <h1 className="text-2xl font-bold">moorestech Web UI</h1>
        <DebugActionButton />
      </div>
      <InventoryPanel />
      <RecipeViewer />
      <ItemListPanel />
      <ToastHost />
    </div>
  );
}
```
（`style` の `gridTemplateAreas`/`Columns`/`Rows` は現状値を維持。）

`RecipeViewer.tsx`: Props を撤廃し store 購読。`key={itemId}` → `key={selectedItemId}` を**必ず維持**:
```tsx
export default function RecipeViewer() {
  const selectedItemId = useUiStore((s) => s.selectedItemId);
  const onSelect = useUiStore((s) => s.setSelectedItem);
  // ...（recipes/machineRecipes/inventory/itemMaster/loaded は不変）
  // itemId === null → 案内文、それ以外は <RecipeContent key={selectedItemId} itemId={selectedItemId} ... onSelect={onSelect} />
}
```
import に `import { useUiStore } from "../app/uiStore";`。`RecipeContent` の `onSelect` は store の setter を渡す（子の素材クリックジャンプ挙動は不変）。

`ItemListPanel.tsx`: Props 撤廃、store 購読:
```tsx
export default function ItemListPanel() {
  const selectedItemId = useUiStore((s) => s.selectedItemId);
  const onSelect = useUiStore((s) => s.setSelectedItem);
  // ...（itemList/itemMaster と JSX は不変。selected={id === selectedItemId} / onLeftDown={() => onSelect(id)}）
}
```

- [ ] **Step 9: ビルド + 全テストで挙動不変を確認**

Run: `pnpm build && pnpm test && pnpm test:e2e`
Expected: 全 PASS。特に recipe.spec（tab/pager リセット）と toast.spec が緑であること。

- [ ] **Step 10: Commit**

```bash
git add moorestech_web/webui/src
git rm moorestech_web/webui/src/bridge/toastBus.ts
git commit -m "refactor(webui): selectedItemId/toast を Zustand 化(grab/recipeIndex は据え置き)"
```

---

## Task 6: フォルダ再編 + CSS module + DebugActionButton dev隔離 + Topics 定数化

最後にファイルを feature 階層へ移動し、複雑な箇所だけ CSS module 化、DebugActionButton を dev-only に隔離する。**ロジックは一切変えない**。`@/` alias で import を書き換える。

**目標構造（設計書 §1）:**
```
src/app/          main.tsx App.tsx App.module.css uiStore.ts DebugActionButton.tsx index.css
src/bridge/       webSocketClient.ts protocol.ts payloadTypes.ts useTopic.ts useItemMaster.ts actions.ts notify.ts index.ts
src/features/inventory/  InventoryPanel/{index.tsx,GrabOverlay.tsx,style.module.css} inventoryLogic.ts(.test) types.ts index.ts
src/features/recipe/     RecipeViewer.tsx CraftRecipeView.tsx MachineRecipeView.tsx RecipePager.tsx ItemHeader.tsx craftLogic.ts(.test) types.ts index.ts
src/features/toast/      ToastHost.tsx toastStore.ts index.ts
src/shared/ui/           ItemSlot/{index.tsx,style.module.css} ItemIcon.tsx index.ts
```

- [ ] **Step 1: app/ へ移動**

`git mv` で:
```bash
mkdir -p src/app
git mv src/App.tsx src/app/App.tsx
git mv src/index.css src/app/index.css
git mv src/components/DebugActionButton.tsx src/app/DebugActionButton.tsx
```
`src/main.tsx` の import を `./app/App`、`./app/index.css` に更新（main.tsx は src 直下のまま）。

- [ ] **Step 2: App の inline grid style を App.module.css へ（複雑な grid-template-areas のみ）**

`src/app/App.module.css`:
```css
.layout {
  display: grid;
  gap: 1.5rem;
  grid-template-areas: "header header header" "inv viewer items" "hotbar hotbar hotbar";
  grid-template-columns: auto 1fr auto;
  grid-template-rows: auto 1fr auto;
}
```
`App.tsx` の外側 div を `className={\`p-4 min-h-screen ${styles.layout}\`}` にし `style={...}` を削除。`import styles from "./App.module.css";` を追加。

- [ ] **Step 3: shared/ui へ ItemIcon / ItemSlot を移動（ItemSlot はフォルダ化 + module）**

```bash
mkdir -p src/shared/ui/ItemSlot
git mv src/components/ItemIcon.tsx src/shared/ui/ItemIcon.tsx
git mv src/components/ItemSlot.tsx src/shared/ui/ItemSlot/index.tsx
```
ItemSlot の tooltip（group-hover の絶対配置）を `src/shared/ui/ItemSlot/style.module.css` に切り出し（任意・複雑なら）。最小では Tailwind のままでも可だが、設計書が tooltip を module 対象に挙げているため切り出す:
```css
.tooltip {
  position: absolute;
  bottom: 100%;
  left: 50%;
  transform: translateX(-50%);
  margin-bottom: 0.25rem;
  white-space: nowrap;
  z-index: 20;
}
```
`index.tsx` の tooltip span を `className={\`hidden group-hover:block bg-black/90 text-white text-xs rounded px-2 py-1 ${styles.tooltip}\`}` にし `import styles from "./style.module.css";`。`ItemIcon` import を `../ItemIcon` に更新。
`src/shared/ui/index.ts`:
```ts
export { default as ItemSlot } from "./ItemSlot";
export { default as ItemIcon } from "./ItemIcon";
```

- [ ] **Step 4: features/inventory へ移動（InventoryPanel フォルダ化 + GrabOverlay 分離 + module）**

```bash
mkdir -p src/features/inventory/InventoryPanel
git mv src/components/InventoryPanel.tsx src/features/inventory/InventoryPanel/index.tsx
```
`GrabOverlay` を `src/features/inventory/InventoryPanel/GrabOverlay.tsx` に切り出し（現関数をそのまま移植、`ItemSlot` import を `@/shared/ui` から）。drag overlay の追従（fixed + left/top）は inline style のままで可（mousePos 動的値のため module 化しない＝設計書の keyframes/固定スタイル限定に合致）。`index.tsx` から GrabOverlay を import。
`src/features/inventory/types.ts`: UI 専用型があれば置く（現状 UI 専用型は無いので空 or 省略可。**ファイルを作らない**＝YAGNI）。
`src/features/inventory/index.ts`:
```ts
export { default as InventoryPanel } from "./InventoryPanel";
```

- [ ] **Step 5: features/recipe へ移動**

```bash
git mv src/components/RecipeViewer.tsx src/features/recipe/RecipeViewer.tsx
git mv src/components/CraftRecipeView.tsx src/features/recipe/CraftRecipeView.tsx
git mv src/components/MachineRecipeView.tsx src/features/recipe/MachineRecipeView.tsx
git mv src/components/RecipePager.tsx src/features/recipe/RecipePager.tsx
git mv src/components/ItemHeader.tsx src/features/recipe/ItemHeader.tsx
```
`src/features/recipe/index.ts`:
```ts
export { default as RecipeViewer } from "./RecipeViewer";
```

- [ ] **Step 6: features/toast へ移動**

```bash
git mv src/components/ToastHost.tsx src/features/toast/ToastHost.tsx
```
`src/features/toast/index.ts`:
```ts
export { default as ToastHost } from "./ToastHost";
export { useToastStore, emitToast } from "./toastStore";
```

- [ ] **Step 7: 旧 types/ を削除（payloadTypes に集約済み）**

全コンポーネントの `../types/inventory` 等 import を `@/bridge/payloadTypes` に置換した上で:
```bash
git rm src/types/inventory.ts src/types/crafting.ts src/types/itemMaster.ts src/types/recipeViewer.ts
```
`useItemMaster.ts` の `import type { ItemMasterData, ItemMasterEntry } from "../types/itemMaster";` を `from "./payloadTypes";` に変更。

- [ ] **Step 8: 全 import を `@/` alias へ統一 + Topics 定数化**

各ファイルの相対 import を `@/bridge/...` `@/features/...` `@/shared/ui` へ書き換え。`useTopic("local_player.inventory")` 等を `useTopic(Topics.inventory)` に置換（`import { Topics } from "@/bridge/protocol";`）。bridge の public API を `src/bridge/index.ts` で公開:
```ts
export { useTopic } from "./useTopic";
export { useItemMaster } from "./useItemMaster";
export { dispatchAction } from "./actions";
export { Topics } from "./protocol";
export type { TopicPayloads, ActionPayloads } from "./protocol";
export type * from "./payloadTypes";
```

- [ ] **Step 9: DebugActionButton を dev-only dynamic import に**

`src/app/App.tsx` から `DebugActionButton` の static import を削除。代わりに:
```tsx
import { lazy, Suspense } from "react";

// dev 専用。static import すると本番バンドルに残るため import.meta.env.DEV 内で lazy 化
// Dev-only; a static import would ship to prod, so lazy-load it inside the import.meta.env.DEV guard
const DebugActionButton = import.meta.env.DEV
  ? lazy(() => import("./DebugActionButton"))
  : null;
```
ヘッダ部:
```tsx
        <h1 className="text-2xl font-bold">moorestech Web UI</h1>
        {DebugActionButton ? (
          <Suspense fallback={null}>
            <DebugActionButton />
          </Suspense>
        ) : null}
```
> e2e は dev build ではなく `vite build`（prod）を配信するため、`toast.spec.ts` の「Ping Action」テストは **prod で消える**。→ Task 6 完了時に `toast.spec.ts` の Ping テストを削除し、トーストは「失敗 action のトースト」で代替検証する spec に差し替える（下 step 10）。

- [ ] **Step 10: toast.spec.ts を prod 互換に差し替え**

`e2e/mock-host/server.ts` は既に `fail.always` を ok=false で返す。`toast.spec.ts` を:
```ts
import { test, expect } from "@playwright/test";

test("失敗 action はエラートーストを出す", async ({ page }) => {
  await page.goto("/");
  await expect(page.getByRole("heading", { name: "Inventory" })).toBeVisible();
  // window 経由でテスト用に失敗 action を直接送る手段が無いため、
  // 右クリックsplit が mock で成功する現状では、craft の素材不足など UI 操作起点の失敗が無い。
  // → DebugActionButton が無い prod では、別途 mock を fail にする専用ルートで検証する。
});
```
> **判断**: prod で UI 起点の失敗 action を作るのは難しい。→ 代替として **mock host に「最初の craft.execute だけ fail させる」モードを query (`?e2e=craftfail`) で切替**え、craft 失敗トーストを検証する。実装が重い場合は **toast 検証は vitest（toastStore 単体）へ寄せ**、e2e の toast spec は削除する。**採用: toastStore の単体テストを追加し、e2e の toast spec は削除**（2層戦略に合致、e2e の守備範囲を過大評価しない）。

`e2e/tests/toast.spec.ts` を削除し、`src/features/toast/toastStore.test.ts` を追加:
```ts
import { describe, it, expect, vi, beforeEach } from "vitest";
import { useToastStore, emitToast } from "./toastStore";

describe("toastStore", () => {
  beforeEach(() => {
    vi.useFakeTimers();
    useToastStore.setState({ toasts: [] });
  });
  it("emitToast で追加され 3秒後に消える", () => {
    emitToast("hello");
    expect(useToastStore.getState().toasts.map((t) => t.message)).toEqual(["hello"]);
    vi.advanceTimersByTime(3000);
    expect(useToastStore.getState().toasts).toEqual([]);
  });
});
```
> `toastStore.ts` は `setTimeout` を使う。vitest fake timers で検証可能。`emitToast` は store 経由なので DOM 不要。

- [ ] **Step 11: ビルド + 全テスト**

Run: `pnpm build && pnpm test && pnpm test:e2e`
Expected: 全 PASS。e2e は inventory/recipe spec が緑（toast spec は削除済み）。

- [ ] **Step 12: Commit**

```bash
git add moorestech_web/webui
git commit -m "refactor(webui): feature階層へ再編 + CSS module + DebugActionButton dev隔離 + @/alias"
```

---

## Task 7: 実装ゲート確認（最終）

**Files:** なし（検証のみ）

- [ ] **Step 1: bridge → features の逆依存が無いこと**

Run: `cd moorestech_web/webui && rg -n "features/" src/bridge`
Expected: **マッチ無し**（出力空）。あれば設計違反 → 該当 import を sink/protocol 経由に直す。

- [ ] **Step 2: DebugActionButton の static import が無いこと**

Run: `rg -n "import .*DebugActionButton" src/app/App.tsx`
Expected: `lazy(() => import("./DebugActionButton"))` のみ（トップレベル static import が無い）。

- [ ] **Step 3: 旧 components/ と types/ が空であること**

Run: `ls src/components src/types 2>/dev/null; echo "exit=$?"`
Expected: ディレクトリ不存在（全移動済み）。残っていれば移動漏れ。

- [ ] **Step 4: 3ゲート総合**

Run: `pnpm build && pnpm test && pnpm test:e2e`
Expected: 全 PASS。

- [ ] **Step 5: 見た目不変の目視（任意）**

Run: `pnpm dev` で実 Unity ホスト接続 or mock で起動し、3カラム+ホットバーのレイアウト・配色が刷新前と一致することを目視。

- [ ] **Step 6: 最終 Commit（あれば）**

```bash
git add -A && git commit -m "chore(webui): フェーズA 実装ゲート確認" || echo "no changes"
```

---

## フェーズB（dblclick collect の host側修正）— 別計画（A完了後に詳細化）

**本計画のスコープ外。** A 完了後、以下を別 plan として詳細化・実装する（C# 変更を含むため `uloop compile` 必須）。

1. **WebUiHost 実機で dblclick 期待値表を確定**（grab保持/素手非空/素手空/右クリック混在）。
2. **`CollectActionHandler`（C#）改修**: payload を `{ slot }` に変更し、`TryParseClickableSlotRef`（grab area 拒否の新 parser）で parse。現在の `GrabInventory` で Grab/slot を host 側 decide。resolved-empty は `Success` no-op。
   - File: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Actions/InventoryActions.cs`
   - `InventoryAreaMapper` に `TryParseClickableSlotRef` を追加（既存 `TryParseSlotRef` は grab を受けるため別メソッド）。
3. **protocol(TS)更新**: `ActionPayloads["inventory.collect"]` を `{ slot: SlotRef }` に変更。`InventoryPanel.onDoubleClick` を簡素化し `clickGrabHistory` を**削除**（Aで温存した履歴を撤去）。mock host の collect 契約を更新。
4. **C#単体テスト**（判定ロジックの本丸）: `CollectActionHandler.ExecuteAsync({slot})` を mock controller で叩き、grab保持→`CollectItems(Grab,0)` / 素手非空→`CollectItems(MainOrSub,slot)` / 素手空→no-op を検証。
5. **dblclick e2e**（payload `{slot}` 契約 + result 後 UI 再描画）が緑になるまで完了としない（**ゲート条件**）。
6. **PRチェックリストに実Unityホスト smoke 手順を1項目追加**（C#↔TS ドリフトは mock では捕捉不能）。
7. `.cs` 変更後は `uloop compile --project-path ./moorestech_client` を必ず実行。

---

## 自己レビュー（writing-plans 完了時チェック結果）

- **spec カバレッジ**: 設計書 §1〜§6 + 実装ゲート + フェーズB を全タスクで被覆。Zustand 範囲(uiStore/toast)・依存方向(notify sink)・型所有(payloadTypes/protocol)・2層テスト・CSS限定・dev隔離・@alias を網羅。
- **placeholder**: TBD/TODO 無し。各 step に実コードと期待出力。
- **型整合**: `resolveDirectMoveTarget`/`buildOwnedCounts`/`craftable`/`clampIndex` の signature はテストと実装で一致。`useTopic<K>`/`dispatchAction<K>`/`subscribeTopic<K>` の制約一貫。`craftable` 関数名衝突は `isCraftable` で回避済み。
- **既知リスク明記**: bridge→features 逆依存を notify sink で回避（Task5 step4-6 で確定）。prod build で DebugActionButton が消えるため toast 検証を vitest へ移管（Task6 step10）。
