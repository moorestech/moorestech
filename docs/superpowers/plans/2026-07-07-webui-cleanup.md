# webui クリーンアップ（3観点レビュー指摘の統合対応）実装計画

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** webui（moorestech_web/webui）に対する3系統のコードレビュー指摘（重複・依存方向・hooks）のうち、実害バグ2件と構造起因の重複・レイヤリング違反を一括解消する。

**Architecture:** 依存方向を `app → features → shared → bridge` の一方向に正す。プレイヤー/ブロック双方のスロット操作ロジックを `shared/itemMove` の純関数プランナに集約し、feature 側は「topic を読んでプランナを呼び dispatch する」薄い配線のみ持つ。app/ に居座っていた横断状態導出は `shared/uiState` へ、recipe ローカル状態は `features/recipe` へ降ろす。

**Tech Stack:** React 18 + zustand 5 + Mantine 8 / vitest（unit）/ Playwright（e2e, mock-host 直列実行）/ pnpm

## Global Constraints

- 作業ディレクトリ: `/Users/katsumi/moorestech-worktrees/tree2/moorestech_web/webui`（web-ui ブランチ）。**全コマンドはここで実行**
- C#・Unity 側のコードは一切変更しない（webui + e2e mock のみ）
- 型チェック兼ビルド: `pnpm build` / unit テスト: `pnpm exec vitest run <path>`（全件は `pnpm test`）/ e2e: `pnpm test:e2e`（単発は `pnpm exec playwright test --config e2e/playwright.config.ts <名前部分一致>`）
- e2e ファイルを変更したら `pnpm exec tsc -p e2e/tsconfig.json --noEmit` で型チェック
- コメントは「// 日本語 → // English」の2行セット（約3〜10行毎）。自明なコメントは書かない
- 1ファイル200行以下・1ディレクトリ10ファイル以下・partial的分割禁止
- import 方向: features → app は禁止。feature 間の直 import は禁止（shared 経由）。feature/app は bridge の barrel（`@/bridge`）経由でアクセス
- コンポーネントは default export（既存 named の ModalHost/ProgressBar は変更しない）
- コミットメッセージは既存ログ準拠の日本語 conventional commit（例: `refactor(webui): ...`）
- e2e は mock-host のグローバル状態を共有するため直列実行（workers=1）。`received` は全テスト横断で蓄積されるので payload は全等値で照合する

## 配置と前例（spec-architecture-review 済み）

| 新規/移動する物 | 配置先 | 前例・根拠 |
|---|---|---|
| `ACTION_TYPES` 定数 | `src/bridge/transport/protocol.ts` | 同ファイルの `Topics`（wire 名の単一真実）と同じパターン |
| `itemMasterStore`（zustand化） | `src/bridge/store/` | `topicStore.ts` が同形（zustand + 命令的アクセサ） |
| `shared/itemMove/*`（移動計画の純関数群） | `src/shared/itemMove/` | `shared/clamp01.ts`（複数featureが使う純関数）。bridge への型依存は `shared/ui/FluidSlot/index.tsx:2` に前例あり |
| `useBlockSlotGestures` | `features/blockInventory/` | `blockInteractionContext.ts`（feature内共有フック）と同居 |
| `shared/uiState/*`（activeLayer・uiScreenRouting・useGameLayerKeydown） | `src/shared/uiState/` | レビュー指摘どおり「複数featureが共有する横断状態導出」で app 所属が不適切 |
| `selectionStore`（旧 uiStore） | `features/recipe/` | `features/toast/toastStore.ts`（feature内 zustand store）が前例 |
| `buildOwnedCounts` | `src/shared/ownedCounts.ts` | `shared/clamp01.ts` と同格の共有純関数 |
| `FluidSlotRow` | `src/shared/ui/FluidSlotRow/` | `FluidSlot`/`ProgressArrow` と同じ shared/ui 配下 |
| `PowerRateText` | `features/blockInventory/details/` | 使用者2つ（MachineSection/MinerSection）が共に details 内 |
| `e2e/support/*` | `e2e/support/` | **新規パターン**（前例なし）。spec 9ファイルの定型排除のため新設 |

## 意思決定の記録（レビュー時の注目点）

1. **半分掴みの統一方針**: プレイヤー側は `inventory.split`（ホスト計算）を**維持**、ブロック側はクライアント床計算を**維持**する。ホスト C# (`InventoryActions.cs` の `SplitGrabActionHandler`) を確認した結果、ホストも `item.Count / 2`（床）で同一セマンティクスであり、現時点の挙動差は無い。ホストに `block_inventory.split` が存在しないためブロック側のホスト委譲は webui 単独では不可能。代わりに両プランナを `shared/itemMove` の同一モジュール群に置き、差異をコメント付きで1箇所に固定する。完全統一（C# へ `block_inventory.split` 追加）は本計画のスコープ外の follow-up。
2. **ModalHost の連打**: C# `WebUiModalService.cs:94` が `TrySetResult` を使用していることを確認済み。2回目の respond は `no_pending_modal`（BENIGN_ERRORS登録済み）で無害。**対応不要としてクローズ**。
3. **TankInventory**: レジストリ未登録は意図的（`blockLogic.test.ts` のコメント「実流体ブロック配線時に再登録する想定」）。削除も登録もせず、FluidSlotRow への置換のみ行う。
4. **fluid 行の progress=null の扱い**: 「非表示」に統一（FluidSlotRow が `progress != null` のときだけ矢印を描く）。MachineSection の fluid 行は従来から矢印なしのため progress を渡さない（入出力グリッド間の ProgressArrow は別物で不変）。

## 対応しないと決めた項目（低価値・意図的スキップ）

- `connecting...` プレースホルダ3箇所の共通化（3行未満の重複）
- スロット枠CSSのCSSモジュール間統合（見た目回帰リスク > 効果）
- ModalHost/ProgressBar の named export 統一（コスメティック）
- ItemIcon の失敗キャッシュ再試行（実質 by-design）

---

### Task 1: ACTION_TYPES を protocol.ts から導出し mock の手書き Set を撲滅

**Files:**
- Modify: `src/bridge/transport/protocol.ts`（末尾に追記）
- Modify: `e2e/mock-host/wsHandler.ts:11-28`

**Interfaces:**
- Produces: `ACTION_TYPES: readonly string[]`（14要素）、`ActionType` 型。以降のタスクは使用しないが、新 action 追加時の単一編集点になる

- [ ] **Step 1: protocol.ts に ACTION_TYPES を追記**

`src/bridge/transport/protocol.ts` の `ActionPayloads` 定義（77行目）の直後に追加:

```ts
// 既知 action type の実行時リスト。ActionPayloads のキーと1:1（下の網羅チェックで担保）
// Runtime list of known action types, 1:1 with ActionPayloads keys (enforced by the check below)
export const ACTION_TYPES = [
  "inventory.move_item",
  "inventory.split",
  "inventory.collect",
  "inventory.sort",
  "inventory.select_hotbar",
  "craft.execute",
  "ui.modal.respond",
  "block_inventory.move_item",
  "block_inventory.collect",
  "ui_state.request",
  "research.complete",
  "filter_splitter.set_mode",
  "filter_splitter.set_filter_item",
  "debug.echo",
] as const satisfies readonly (keyof ActionPayloads)[];

export type ActionType = (typeof ACTION_TYPES)[number];

// ActionPayloads にあって ACTION_TYPES に無いキーがあると never 制約違反でコンパイルエラーになる
// A key in ActionPayloads missing from ACTION_TYPES violates the never constraint and fails to compile
type AssertNever<T extends never> = T;
export type ActionTypesExhaustive = AssertNever<Exclude<keyof ActionPayloads, ActionType>>;
```

`satisfies` が「リストに存在しないキーの混入」を、`AssertNever` が「キーの列挙漏れ」を、それぞれコンパイルエラーにする。

- [ ] **Step 2: wsHandler.ts の KNOWN_ACTIONS を導出に置換**

`e2e/mock-host/wsHandler.ts` の import 行を変更:

```ts
import { Topics, ACTION_TYPES } from "../../src/bridge/transport/protocol";
```

13〜28行の手書き Set を1行に置換:

```ts
// 本番 dispatcher が受理する既知 action type。protocol.ts から導出し二重定義を排除する
// Action types the real dispatcher accepts, derived from protocol.ts to kill the duplicate list
const KNOWN_ACTIONS = new Set<string>(ACTION_TYPES);
```

- [ ] **Step 3: 型チェックが両方向に効くことを確認**

Run: `pnpm build` → PASS。
次に一時的に `ACTION_TYPES` から `"debug.echo",` の行を削除して `pnpm exec tsc -p e2e/tsconfig.json --noEmit` を実行し、`ActionTypesExhaustive` でエラーになることを確認したら、削除した行を**元に戻す**。

- [ ] **Step 4: e2e 型チェックとスモーク**

Run: `pnpm exec tsc -p e2e/tsconfig.json --noEmit` → PASS
Run: `pnpm exec playwright test --config e2e/playwright.config.ts uiState` → PASS（unknown_action 拒否経路の回帰確認）

- [ ] **Step 5: Commit**

```bash
git add src/bridge/transport/protocol.ts e2e/mock-host/wsHandler.ts
git commit -m "refactor(webui): ACTION_TYPESをprotocol.tsへ一元化しmockの手書きSetを導出に置換"
```

---

### Task 2: itemMaster を zustand ストア化し失敗時自動リトライ（非リアクティブキャッシュバグの修正）

常時マウントの HotbarPanel/BlockInventoryPanel が、ゲーム起動前の 503 を踏むと永遠に itemMaster=null のまま固まるバグの修正。

**Files:**
- Create: `src/bridge/store/itemMasterStore.ts`
- Create: `src/bridge/store/itemMasterStore.test.ts`
- Modify: `src/bridge/store/useItemMaster.ts`（全面書き換え）
- Modify: deep import の正規化 — `src/features/inventory/InventoryPanel/index.tsx:3` / `src/features/inventory/HotbarPanel/index.tsx:3` / `src/features/blockInventory/BlockInventoryPanel.tsx:4` / `src/features/recipe/RecipeViewer.tsx:3` / `src/features/recipe/views/ItemListPanel.tsx:3`

**Interfaces:**
- Produces: `useItemMaster(): Map<number, ItemMasterEntry> | null`（シグネチャ既存互換・呼び出し側変更不要）、`ensureItemMasterLoaded(): void`、`useItemMasterStore`（zustand）

- [ ] **Step 1: 失敗するテストを書く**

`src/bridge/store/itemMasterStore.test.ts`:

```ts
import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";

// モジュール変数(started)を各テストで初期化するため resetModules + 動的 import を使う
// Reset module-level state (started) per test via resetModules + dynamic import
beforeEach(() => {
  vi.useFakeTimers();
  vi.resetModules();
});
afterEach(() => {
  vi.useRealTimers();
  vi.unstubAllGlobals();
});

const masterJson = { items: [{ itemId: 1, name: "Wood", maxStack: 100 }] };

describe("ensureItemMasterLoaded", () => {
  it("初回成功で master がストアへ反映される", async () => {
    vi.stubGlobal("fetch", vi.fn().mockResolvedValue({ ok: true, json: async () => masterJson }));
    const { ensureItemMasterLoaded, useItemMasterStore } = await import("./itemMasterStore");
    ensureItemMasterLoaded();
    await vi.advanceTimersByTimeAsync(0);
    expect(useItemMasterStore.getState().master?.get(1)?.maxStack).toBe(100);
  });

  it("503 の後もマウントに依存せず自動再試行して反映される", async () => {
    const fetchMock = vi
      .fn()
      .mockResolvedValueOnce({ ok: false })
      .mockResolvedValueOnce({ ok: true, json: async () => masterJson });
    vi.stubGlobal("fetch", fetchMock);
    const { ensureItemMasterLoaded, useItemMasterStore } = await import("./itemMasterStore");
    ensureItemMasterLoaded();
    await vi.advanceTimersByTimeAsync(0);
    expect(useItemMasterStore.getState().master).toBeNull();
    // リトライ間隔(3秒)経過で2回目のfetchが成功する
    // After the 3s retry interval the second fetch succeeds
    await vi.advanceTimersByTimeAsync(3000);
    expect(useItemMasterStore.getState().master?.get(1)?.name).toBe("Wood");
  });

  it("ネットワーク例外でも再試行する", async () => {
    const fetchMock = vi
      .fn()
      .mockRejectedValueOnce(new Error("net down"))
      .mockResolvedValueOnce({ ok: true, json: async () => masterJson });
    vi.stubGlobal("fetch", fetchMock);
    const { ensureItemMasterLoaded, useItemMasterStore } = await import("./itemMasterStore");
    ensureItemMasterLoaded();
    await vi.advanceTimersByTimeAsync(3000);
    expect(useItemMasterStore.getState().master?.get(1)?.name).toBe("Wood");
  });

  it("多重呼び出しでも fetch は1系列しか走らない", async () => {
    const fetchMock = vi.fn().mockResolvedValue({ ok: true, json: async () => masterJson });
    vi.stubGlobal("fetch", fetchMock);
    const { ensureItemMasterLoaded } = await import("./itemMasterStore");
    ensureItemMasterLoaded();
    ensureItemMasterLoaded();
    await vi.advanceTimersByTimeAsync(0);
    expect(fetchMock).toHaveBeenCalledTimes(1);
  });
});
```

- [ ] **Step 2: テストが失敗することを確認**

Run: `pnpm exec vitest run src/bridge/store/itemMasterStore.test.ts`
Expected: FAIL（`Cannot find module './itemMasterStore'`）

- [ ] **Step 3: itemMasterStore.ts を実装**

`src/bridge/store/itemMasterStore.ts`:

```ts
import { create } from "zustand";
import type { ItemMasterData, ItemMasterEntry } from "../contract/payloadTypes";

type ItemMasterState = {
  master: Map<number, ItemMasterEntry> | null;
  setMaster: (master: Map<number, ItemMasterEntry>) => void;
};

// アイテムマスタの zustand ストア。常時マウントのコンポーネントにも遅延ロードがリアクティブに届く
// Zustand store for the item master; late loads reach always-mounted components reactively
export const useItemMasterStore = create<ItemMasterState>((set) => ({
  master: null,
  setMaster: (master) => set({ master }),
}));

// ゲーム起動前の 503 やネットワーク断は、マウントに依存せず一定間隔で自動再試行する
// Retry on a fixed interval independent of mounts (e.g. 503 before game start, network drop)
const RETRY_INTERVAL_MS = 3000;
let started = false;

export function ensureItemMasterLoaded(): void {
  if (started) return;
  started = true;
  void loadWithRetry();
}

async function loadWithRetry(): Promise<void> {
  for (;;) {
    const res = await fetch("/api/master/items").catch(() => null);
    if (res?.ok) {
      const data: ItemMasterData = await res.json();
      useItemMasterStore.getState().setMaster(new Map(data.items.map((i) => [i.itemId, i])));
      return;
    }
    await new Promise((resolve) => setTimeout(resolve, RETRY_INTERVAL_MS));
  }
}
```

- [ ] **Step 4: useItemMaster.ts を書き換え**

`src/bridge/store/useItemMaster.ts` 全体を以下に置換:

```ts
import { useEffect } from "react";
import type { ItemMasterEntry } from "../contract/payloadTypes";
import { ensureItemMasterLoaded, useItemMasterStore } from "./itemMasterStore";

// アイテムマスタを購読する React フック。未ロード中は null（ロード完了時に自動再レンダー）
// React hook subscribing to the item master; null while unloaded, re-renders automatically on load
export function useItemMaster(): Map<number, ItemMasterEntry> | null {
  useEffect(() => {
    ensureItemMasterLoaded();
  }, []);
  return useItemMasterStore((s) => s.master);
}
```

- [ ] **Step 5: テストと型チェック**

Run: `pnpm exec vitest run src/bridge/store/itemMasterStore.test.ts` → PASS（4件）
Run: `pnpm build` → PASS

- [ ] **Step 6: deep import を barrel 経由に正規化**

以下5ファイルの `import { useItemMaster } from "@/bridge/store/useItemMaster";` を削除し、既存の `@/bridge` import に `useItemMaster` を追加する（`src/bridge/index.ts` は既に export 済み）:

- `src/features/inventory/InventoryPanel/index.tsx` → `import { useTopic, dispatchAction, Topics, useItemMaster } from "@/bridge";`
- `src/features/inventory/HotbarPanel/index.tsx` → `import { useTopic, useTopicSelector, readTopic, dispatchAction, Topics, useItemMaster } from "@/bridge";`
- `src/features/blockInventory/BlockInventoryPanel.tsx` → `import { useTopic, useTopicSelector, Topics, dispatchAction, useItemMaster } from "@/bridge";`
- `src/features/recipe/RecipeViewer.tsx` → `import { useTopic, Topics, useItemMaster } from "@/bridge";`
- `src/features/recipe/views/ItemListPanel.tsx` → `import { useTopic, Topics, useItemMaster } from "@/bridge";`

確認: `grep -rn "bridge/store/useItemMaster" src/` が0件になること。

- [ ] **Step 7: 全体テストと e2e スモーク**

Run: `pnpm test` → PASS
Run: `pnpm exec playwright test --config e2e/playwright.config.ts inventory` → PASS

- [ ] **Step 8: Commit**

```bash
git add src/bridge/store/ src/features/
git commit -m "fix(webui): itemMasterをzustandストア化し503後の自動リトライで常時マウントUIの名前欠落を修正"
```

---

### Task 3: GrabOverlay の掴んだ瞬間の stale 座標を修正

掴んだ直後のフレームが (0,0) や前回座標で描画される視覚バグ。grab は必ず mousedown で始まる性質を使い、mousedown 座標を常時追跡して初期値にする。

**Files:**
- Modify: `src/features/inventory/InventoryPanel/GrabOverlay.tsx`
- Create: `e2e/tests/grabOverlay.spec.ts`

- [ ] **Step 1: 失敗する e2e を書く**

`e2e/tests/grabOverlay.spec.ts`:

```ts
import { test, expect } from "@playwright/test";

// 掴んだ瞬間からオーバーレイがカーソル位置に出ることを検証する（stale座標回帰の防止）
// Assert the overlay appears at the cursor from the very first held frame (guards the stale-position regression)
test("アイテムを掴んだ瞬間にオーバーレイがクリック座標へ表示される", async ({ page }) => {
  await page.goto("/");
  const slot = page.getByTestId("main-grid").locator("> div").first();
  await expect(slot).toBeVisible();
  const box = (await slot.boundingBox())!;
  await slot.click();
  const overlay = page.getByTestId("grab-overlay");
  await expect(overlay).toBeVisible();
  const overlayBox = (await overlay.boundingBox())!;
  // オーバーレイ原点はカーソル-24px。クリックはスロット中央なので 中央-24±2px に出るはず
  // The overlay origin is cursor-24px; the click hits the slot center, so expect center-24 (±2px)
  expect(Math.abs(overlayBox.x - (box.x + box.width / 2 - 24))).toBeLessThanOrEqual(2);
  expect(Math.abs(overlayBox.y - (box.y + box.height / 2 - 24))).toBeLessThanOrEqual(2);
});
```

- [ ] **Step 2: 失敗を確認**

Run: `pnpm exec playwright test --config e2e/playwright.config.ts grabOverlay`
Expected: FAIL（オーバーレイが画面左上 (-24,-24) 付近に出るため座標アサートが落ちる）

- [ ] **Step 3: GrabOverlay.tsx を修正**

全体を以下に置換:

```tsx
import { useLayoutEffect, useState } from "react";
import { ItemSlot } from "@/shared/ui";
import type { SlotData } from "@/bridge/contract/payloadTypes";
import styles from "./GrabOverlay.module.css";

// 掴み開始座標の供給源。grab は必ず mousedown で始まるため mousedown のみ常時追跡する（setState 無しなので再レンダー無し）
// Source of the grab-start position; grabs always begin with a mousedown, so track only mousedown (no setState, no re-render)
let lastPointerDown = { x: 0, y: 0 };
if (typeof window !== "undefined") {
  window.addEventListener(
    "mousedown",
    (e) => {
      lastPointerDown = { x: e.clientX, y: e.clientY };
    },
    { capture: true },
  );
}

// マウス追従の grab オーバーレイ。mousemove の再レンダリングをこのコンポーネント内に閉じ込める
// Cursor-following grab overlay; keeps mousemove re-renders contained to this component
export default function GrabOverlay({ grab }: { grab: SlotData }) {
  const [mousePos, setMousePos] = useState(lastPointerDown);

  // 掴んでいる間だけ mousemove 追従。掴んだ瞬間は描画前に mousedown 座標へ同期する（stale座標の一瞬表示を防ぐ）
  // Follow mousemove only while held; sync to the mousedown position before paint when a grab starts
  useLayoutEffect(() => {
    if (grab.count === 0) return;
    setMousePos(lastPointerDown);
    const onMove = (e: globalThis.MouseEvent) => setMousePos({ x: e.clientX, y: e.clientY });
    window.addEventListener("mousemove", onMove);
    return () => window.removeEventListener("mousemove", onMove);
  }, [grab.count]);

  if (grab.count === 0) return null;

  // 追従位置はカーソル座標の動的値なので inline style（module 化対象外）
  // Follow position is a dynamic cursor value, so inline style (not module-ized)
  return (
    <div data-testid="grab-overlay" className={styles.overlay} style={{ left: mousePos.x - 24, top: mousePos.y - 24 }}>
      <ItemSlot itemId={grab.itemId} count={grab.count} />
    </div>
  );
}
```

補足: `grab.count` が変わる度に effect が再実行され座標が `lastPointerDown` に戻るが、count の変化は常に直前の mousedown（スロット操作）由来なので実カーソル位置と一致する。

- [ ] **Step 4: e2e が通ることを確認**

Run: `pnpm exec tsc -p e2e/tsconfig.json --noEmit` → PASS
Run: `pnpm exec playwright test --config e2e/playwright.config.ts grabOverlay inventory` → PASS

- [ ] **Step 5: Commit**

```bash
git add src/features/inventory/InventoryPanel/GrabOverlay.tsx e2e/tests/grabOverlay.spec.ts
git commit -m "fix(webui): 掴んだ瞬間のGrabOverlayをmousedown座標で初期化しstale座標の一瞬表示を修正"
```

---

### Task 4: e2e/support ヘルパ新設と spec の定型一掃

`ActionRecord` 再定義（9ファイル）・`/__actions` 取得定型（約13箇所）・制御エンドポイント生リテラルを `e2e/support/` に集約。あわせて mock の `applyMove` を `applyBlockMove` へ委譲統合する。

**Files:**
- Create: `e2e/support/actions.ts`
- Create: `e2e/support/mockControl.ts`
- Modify: `e2e/tests/*.spec.ts`（7ファイル）と `e2e/tests/block/*.spec.ts`（5ファイル）
- Modify: `e2e/mock-host/inventoryModel.ts:28-41`

**Interfaces:**
- Produces: `payloadsOf(page, type): Promise<unknown[]>`、`ActionRecord` 型、`setBlock(page, type)` / `setModal(page, show)` / `setUiState(page, state)` / `resetResearch(page)`。以降の e2e タスク（Task 3 は先行のため対象外、Task 7 の machine spec）はこれを使う

- [ ] **Step 1: support ヘルパを作成**

`e2e/support/actions.ts`:

```ts
import type { Page } from "@playwright/test";

export type ActionRecord = { type: string; payload: unknown };

// 指定 type の action payload 一覧。received は全テスト横断で蓄積されるため、呼び出し側は全等値で照合する
// Payloads of a given action type; received accumulates across tests, so callers must match by full equality
export async function payloadsOf(page: Page, type: string): Promise<unknown[]> {
  const actions: ActionRecord[] = await page.request.get("/__actions").then((r) => r.json());
  return actions.filter((a) => a.type === type).map((a) => a.payload);
}

// 全 action レコードを返す（type 以外の検証や件数比較用）
// Return every recorded action (for non-type assertions and count checks)
export async function allActions(page: Page): Promise<ActionRecord[]> {
  return page.request.get("/__actions").then((r) => r.json());
}
```

`e2e/support/mockControl.ts`:

```ts
import type { Page } from "@playwright/test";

// mock-host 制御エンドポイントの薄いラッパ。URL リテラルの散在を防ぐ
// Thin wrappers over the mock-host control endpoints; keeps URL literals in one place
export function setBlock(page: Page, type: string) {
  return page.request.get(`/__block?type=${type}`);
}

export function setModal(page: Page, show: boolean) {
  return page.request.get(`/__modal?show=${show ? 1 : 0}`);
}

export function setUiState(page: Page, state: string) {
  return page.request.get(`/__uistate?state=${state}`);
}

export function resetResearch(page: Page) {
  return page.request.get("/__research");
}
```

- [ ] **Step 2: 代表例として blockInventoryGestures.spec.ts を書き換え**

`e2e/tests/block/blockInventoryGestures.spec.ts` の冒頭（1〜21行）を以下に置換（テスト本体は不変）:

```ts
import { test, expect } from "@playwright/test";
import { payloadsOf } from "../../support/actions";
import { setBlock } from "../../support/mockControl";

// 各テスト冒頭で chest を配信状態へリセットし、終了後は閉に戻して後続へ漏らさない
// Reset to chest before each test and back to closed afterwards so state never leaks
test.beforeEach(async ({ page }) => {
  await setBlock(page, "chest");
  await page.goto("/");
  await expect(page.getByTestId("block-inventory")).toBeVisible();
});
test.afterEach(async ({ page }) => {
  await setBlock(page, "closed");
});
```

- [ ] **Step 3: 残りの spec を同じ規則で一掃**

対象: `e2e/tests/` 直下の `hotbar` `inventory` `modal` `recipe` `research` `uiState` `grabOverlay` と `e2e/tests/block/` の `blockDetails` `blockInventory` `filterSplitter` `fluidSlot` 各 spec。機械的規則:

1. ローカルの `type ActionRecord = ...` 定義を削除し、必要なら `import { payloadsOf, allActions, type ActionRecord } from "../support/actions";`（block/ 配下は `../../support/actions`）を追加
2. `page.request.get("/__actions")` を含むインライン定型（filter/map の自前実装）を `payloadsOf` / `allActions` 呼び出しに置換
3. `page.request.get("/__block?type=...")` → `setBlock(page, "...")`、`/__modal?show=...` → `setModal(page, true|false)`、`/__uistate?state=...` → `setUiState(page, "...")`、`/__research` → `resetResearch(page)` に置換（import は `../support/mockControl` / `../../support/mockControl`）

完了判定（すべて0件になること）:

```bash
grep -rn "type ActionRecord" e2e/tests/
grep -rn '__actions\|__block\|__modal\|__uistate\|__research' e2e/tests/
```

- [ ] **Step 4: mock の applyMove を applyBlockMove へ委譲**

`e2e/mock-host/inventoryModel.ts` の `applyMove`（28〜41行）を以下に置換:

```ts
// from の count 個を to へ移す最小モデル。block を跨がない移動は「閉ブロック」扱いの applyBlockMove と完全同型
// Minimal move model; a non-block move is exactly applyBlockMove against a closed block
export function applyMove(inv: PlayerInventoryData, p: ActionPayloads["inventory.move_item"]): string | null {
  return applyBlockMove(inv, { open: false }, p);
}
```

（`applyBlockMove` の定義より後に来るよう、関数の位置を `applyBlockMove` の直後へ移動する）

- [ ] **Step 5: e2e 全件で回帰確認**

Run: `pnpm exec tsc -p e2e/tsconfig.json --noEmit` → PASS
Run: `pnpm test:e2e` → PASS（全spec）

- [ ] **Step 6: Commit**

```bash
git add e2e/
git commit -m "refactor(webui): e2e/supportヘルパを新設しActionRecord再定義と制御URLリテラルを一掃"
```

---

### Task 5: shared/itemMove 新設（移動計画ロジックの共有ドメイン化）

`planDirectMoves` を features/inventory から昇格し、プレイヤー/ブロック双方のクリック操作を「純関数プランナ → PlannedAction[]」に統一する。この時点では**新モジュールの追加とテストのみ**（配線切替は Task 6/7）。

**Files:**
- Create: `src/shared/itemMove/plannedAction.ts`
- Create: `src/shared/itemMove/dispatchPlanned.ts`
- Create: `src/shared/itemMove/planDirectMoves.ts`（`src/features/inventory/inventoryLogic.ts` から移設）
- Create: `src/shared/itemMove/playerSlotPlan.ts`
- Create: `src/shared/itemMove/blockSlotPlan.ts`
- Create: `src/shared/itemMove/index.ts`
- Create: `src/shared/itemMove/planDirectMoves.test.ts`（`inventoryLogic.test.ts` から移設）
- Create: `src/shared/itemMove/playerSlotPlan.test.ts`
- Create: `src/shared/itemMove/blockSlotPlan.test.ts`
- Delete: `src/features/inventory/inventoryLogic.ts` / `src/features/inventory/inventoryLogic.test.ts`（移設完了後）
- Modify: `src/features/inventory/slotActions.ts:3` / `src/features/blockInventory/blockLogic.ts:4`（import 行のみ shared へ向け替え）

**Interfaces:**
- Produces:
  - `type PlannedAction = { type: K; payload: ActionPayloads[K] }`（K は全 action の分配ユニオン）
  - `dispatchPlanned(planned: PlannedAction[]): void`
  - `planDirectMoves(sourceCount, itemId, maxStack, targetSlots): PlannedMove[]`（既存シグネチャ不変）
  - `planPlayerLeftClick(ref, slot, shiftKey, ctx: PlayerSlotContext): PlannedAction[]` / `planPlayerRightClick(ref, slot, grabCount): PlannedAction[]` / `planPlayerDoubleClick(ref): PlannedAction[]`
  - `planBlockLeftClick(index, slot, shiftKey, ctx: BlockSlotContext): PlannedAction[]` / `planBlockRightClick(index, slot, grabCount): PlannedAction[]` / `planBlockDoubleClick(index): PlannedAction[]`
  - `GRAB: SlotRef`（`{ area: "grab", slot: 0 }`）

- [ ] **Step 1: 型とディスパッチャを作成**

`src/shared/itemMove/plannedAction.ts`:

```ts
import type { ActionPayloads } from "@/bridge/transport/protocol";

// 「送るべき action」の計画表現。type と payload の対応が型で相関する分配ユニオン
// A planned action to send; a distributive union correlating type with its payload
export type PlannedAction = {
  [K in keyof ActionPayloads]: { type: K; payload: ActionPayloads[K] };
}[keyof ActionPayloads];
```

`src/shared/itemMove/dispatchPlanned.ts`:

```ts
import { dispatchAction } from "@/bridge";
import type { ActionPayloads } from "@/bridge";
import type { PlannedAction } from "./plannedAction";

// 相関の取れた type/payload 組だけを受けるヘルパ。union のまま dispatchAction へ渡すための橋
// Helper taking only correlated type/payload pairs; bridges the union into dispatchAction
const dispatchOne = <K extends keyof ActionPayloads>(action: { type: K; payload: ActionPayloads[K] }) =>
  dispatchAction(action.type, action.payload);

// 計画された action 列を順に送信する。ack を待たず投げ切り、表示更新は topic event に委ねる
// Fire the planned actions in order without awaiting acks; rendering follows topic events
export function dispatchPlanned(planned: PlannedAction[]): void {
  for (const action of planned) void dispatchOne(action);
}
```

- [ ] **Step 2: planDirectMoves を移設**

`src/features/inventory/inventoryLogic.ts` の内容をそのまま `src/shared/itemMove/planDirectMoves.ts` へコピーし（import は `@/bridge/contract/payloadTypes` のままで動く）、`src/features/inventory/inventoryLogic.test.ts` を `src/shared/itemMove/planDirectMoves.test.ts` へ移して import を `./planDirectMoves` に変更する。元の2ファイルはまだ削除しない（Step 6）。

- [ ] **Step 3: プレイヤープランナのテストを書く**

`src/shared/itemMove/playerSlotPlan.test.ts`（現行 `slotActions.ts` の挙動をそのまま仕様化する）:

```ts
import { describe, it, expect } from "vitest";
import { GRAB, planPlayerLeftClick, planPlayerRightClick, planPlayerDoubleClick } from "./playerSlotPlan";
import type { PlayerSlotContext } from "./playerSlotPlan";
import type { PlayerInventoryData } from "@/bridge/contract/payloadTypes";

const slot = (itemId: number, count: number) => ({ itemId, count });
const inv = (grabCount: number): PlayerInventoryData => ({
  mainSlots: [slot(1, 98), slot(0, 0)],
  hotbarSlots: [slot(0, 0)],
  grab: grabCount > 0 ? slot(9, grabCount) : slot(0, 0),
  selectedHotbar: 0,
});
const ctx = (grabCount: number, blockItemSlots: { itemId: number; count: number }[] | null): PlayerSlotContext => ({
  inventory: inv(grabCount),
  maxStack: 100,
  blockItemSlots,
});

describe("planPlayerLeftClick", () => {
  it("grab保持中は grab 全量をクリックスロットへ置く", () => {
    expect(planPlayerLeftClick({ area: "main", slot: 1 }, slot(0, 0), false, ctx(4, null))).toEqual([
      { type: "inventory.move_item", payload: { from: GRAB, to: { area: "main", slot: 1 }, count: 4 } },
    ]);
  });
  it("空手+空スロットは無操作", () => {
    expect(planPlayerLeftClick({ area: "main", slot: 1 }, slot(0, 0), false, ctx(0, null))).toEqual([]);
  });
  it("空手+中身ありは全量を grab へ拾う", () => {
    expect(planPlayerLeftClick({ area: "main", slot: 0 }, slot(1, 98), false, ctx(0, null))).toEqual([
      { type: "inventory.move_item", payload: { from: { area: "main", slot: 0 }, to: GRAB, count: 98 } },
    ]);
  });
  it("Shift+クリックはブロック開時 block へ配分する", () => {
    const blockSlots = [slot(1, 99), slot(0, 0)];
    expect(planPlayerLeftClick({ area: "main", slot: 0 }, slot(1, 5), true, ctx(0, blockSlots))).toEqual([
      { type: "block_inventory.move_item", payload: { from: { area: "main", slot: 0 }, to: { area: "block", slot: 0 }, count: 1 } },
      { type: "block_inventory.move_item", payload: { from: { area: "main", slot: 0 }, to: { area: "block", slot: 1 }, count: 4 } },
    ]);
  });
  it("Shift+クリックはブロック閉時に反対エリア（main→hotbar）へ配分する", () => {
    expect(planPlayerLeftClick({ area: "main", slot: 0 }, slot(1, 5), true, ctx(0, null))).toEqual([
      { type: "inventory.move_item", payload: { from: { area: "main", slot: 0 }, to: { area: "hotbar", slot: 0 }, count: 5 } },
    ]);
  });
  it("hotbar からの Shift は main へ向かう", () => {
    expect(planPlayerLeftClick({ area: "hotbar", slot: 0 }, slot(1, 1), true, ctx(0, null))).toEqual([
      { type: "inventory.move_item", payload: { from: { area: "hotbar", slot: 0 }, to: { area: "main", slot: 0 }, count: 1 } },
    ]);
  });
});

describe("planPlayerRightClick", () => {
  it("grab保持中はクリックスロットへ1個置く", () => {
    expect(planPlayerRightClick({ area: "main", slot: 1 }, slot(0, 0), 4)).toEqual([
      { type: "inventory.move_item", payload: { from: GRAB, to: { area: "main", slot: 1 }, count: 1 } },
    ]);
  });
  it("空手+中身ありは inventory.split（半分掴みはホスト計算）", () => {
    expect(planPlayerRightClick({ area: "main", slot: 0 }, slot(1, 7), 0)).toEqual([
      { type: "inventory.split", payload: { from: { area: "main", slot: 0 } } },
    ]);
  });
  it("空手+空スロットは無操作", () => {
    expect(planPlayerRightClick({ area: "main", slot: 1 }, slot(0, 0), 0)).toEqual([]);
  });
});

describe("planPlayerDoubleClick", () => {
  it("クリックスロットを送るだけ（収集先はホストが grab 状態で決める）", () => {
    expect(planPlayerDoubleClick({ area: "hotbar", slot: 2 })).toEqual([
      { type: "inventory.collect", payload: { slot: { area: "hotbar", slot: 2 } } },
    ]);
  });
});
```

- [ ] **Step 4: ブロックプランナのテストを書く**

`src/shared/itemMove/blockSlotPlan.test.ts`（現行 `blockLogic.test.ts` の純関数部分を PlannedAction 形式へ移植）:

```ts
import { describe, it, expect } from "vitest";
import { planBlockLeftClick, planBlockRightClick, planBlockDoubleClick } from "./blockSlotPlan";
import type { BlockSlotContext } from "./blockSlotPlan";

const slot = (itemId: number, count: number) => ({ itemId, count });
const ctx = (grabCount: number, mainSlots = [slot(0, 0)]): BlockSlotContext => ({
  grabCount,
  maxStack: 100,
  mainSlots,
});

describe("planBlockLeftClick", () => {
  it("grab保持中は grab 全量を block スロットへ置く（空スロットでも置く）", () => {
    expect(planBlockLeftClick(1, slot(0, 0), false, ctx(4))).toEqual([
      { type: "block_inventory.move_item", payload: { from: { area: "grab", slot: 0 }, to: { area: "block", slot: 1 }, count: 4 } },
    ]);
  });
  it("空手+中身ありは全量を grab へ拾う", () => {
    expect(planBlockLeftClick(2, slot(10, 6), false, ctx(0))).toEqual([
      { type: "block_inventory.move_item", payload: { from: { area: "block", slot: 2 }, to: { area: "grab", slot: 0 }, count: 6 } },
    ]);
  });
  it("空手+空スロットは無操作", () => {
    expect(planBlockLeftClick(3, slot(0, 0), false, ctx(0))).toEqual([]);
  });
  it("Shift+クリックは main の同種スタック→空きの順に配分する", () => {
    const mainSlots = [slot(1, 98), slot(0, 0)];
    expect(planBlockLeftClick(4, slot(1, 7), true, ctx(0, mainSlots))).toEqual([
      { type: "block_inventory.move_item", payload: { from: { area: "block", slot: 4 }, to: { area: "main", slot: 0 }, count: 2 } },
      { type: "block_inventory.move_item", payload: { from: { area: "block", slot: 4 }, to: { area: "main", slot: 1 }, count: 5 } },
    ]);
  });
  it("grab保持中の Shift は通常の置きと同じ（uGUI同様 grab が優先）", () => {
    expect(planBlockLeftClick(0, slot(1, 7), true, ctx(4))).toEqual([
      { type: "block_inventory.move_item", payload: { from: { area: "grab", slot: 0 }, to: { area: "block", slot: 0 }, count: 4 } },
    ]);
  });
});

describe("planBlockRightClick", () => {
  it("grab保持中は block スロットへ1個置く", () => {
    expect(planBlockRightClick(2, slot(0, 0), 5)).toEqual([
      { type: "block_inventory.move_item", payload: { from: { area: "grab", slot: 0 }, to: { area: "block", slot: 2 }, count: 1 } },
    ]);
  });
  it("空手+2個以上は半分(切り捨て)を grab へ拾う", () => {
    expect(planBlockRightClick(0, slot(1, 7), 0)).toEqual([
      { type: "block_inventory.move_item", payload: { from: { area: "block", slot: 0 }, to: { area: "grab", slot: 0 }, count: 3 } },
    ]);
  });
  it("空手+1個は半分が0のため無操作(uGUI準拠)", () => {
    expect(planBlockRightClick(0, slot(1, 1), 0)).toEqual([]);
  });
  it("空手+空スロットは無操作", () => {
    expect(planBlockRightClick(0, slot(0, 0), 0)).toEqual([]);
  });
});

describe("planBlockDoubleClick", () => {
  it("クリックスロットを送るだけ（収集先はホストが grab 状態で決める）", () => {
    expect(planBlockDoubleClick(1)).toEqual([
      { type: "block_inventory.collect", payload: { slot: { area: "block", slot: 1 } } },
    ]);
  });
});
```

- [ ] **Step 5: 失敗を確認してからプランナを実装**

Run: `pnpm exec vitest run src/shared/itemMove/` → FAIL（playerSlotPlan/blockSlotPlan が未実装）

`src/shared/itemMove/playerSlotPlan.ts`:

```ts
import type { InventoryArea, PlayerInventoryData, SlotData, SlotRef } from "@/bridge/contract/payloadTypes";
import { planDirectMoves } from "./planDirectMoves";
import type { PlannedAction } from "./plannedAction";

export const GRAB: SlotRef = { area: "grab", slot: 0 };

// プレイヤースロット操作の判定材料。blockItemSlots はブロックUI開時のみ非null（Shift配分の宛先になる）
// Inputs for player-slot decisions; blockItemSlots is non-null only while a block UI is open (Shift target)
export type PlayerSlotContext = {
  inventory: PlayerInventoryData;
  maxStack: number | undefined;
  blockItemSlots: SlotData[] | null;
};

// 左クリック: grab保持なら全量置き / Shiftなら配分移動 / 中身ありなら全量掴み
// Left click: place all while holding grab / allocate on Shift / pick the whole stack when filled
export function planPlayerLeftClick(ref: SlotRef, slot: SlotData, shiftKey: boolean, ctx: PlayerSlotContext): PlannedAction[] {
  const grabCount = ctx.inventory.grab.count;
  if (grabCount > 0) return [{ type: "inventory.move_item", payload: { from: GRAB, to: ref, count: grabCount } }];
  if (slot.count === 0) return [];
  if (shiftKey) return planShiftMove(ref, slot, ctx);
  return [{ type: "inventory.move_item", payload: { from: ref, to: GRAB, count: slot.count } }];
}

// 右クリック: grab保持なら1個置き / 空手なら inventory.split（半分掴みはホスト計算。stale な client 数量に依存しない）
// Right click: place one while holding grab / inventory.split empty-handed (the host computes the half; no stale client count)
export function planPlayerRightClick(ref: SlotRef, slot: SlotData, grabCount: number): PlannedAction[] {
  if (grabCount > 0) return [{ type: "inventory.move_item", payload: { from: GRAB, to: ref, count: 1 } }];
  if (slot.count === 0) return [];
  return [{ type: "inventory.split", payload: { from: ref } }];
}

// ダブルクリック: 収集先（grab かクリックスロットか）はホストが自身の grab 状態で決める
// Double click: the host decides the target (grab vs clicked slot) from its own grab state
export function planPlayerDoubleClick(ref: SlotRef): PlannedAction[] {
  return [{ type: "inventory.collect", payload: { slot: ref } }];
}

// Shift+クリック: ブロックUIが開いていれば block へ、閉なら反対エリアへ配分する（uGUI DirectMover 準拠）
// Shift-click: allocate into the block while its UI is open, else into the opposite area (mirrors uGUI DirectMover)
function planShiftMove(from: SlotRef, slot: SlotData, ctx: PlayerSlotContext): PlannedAction[] {
  if (ctx.blockItemSlots) {
    return planDirectMoves(slot.count, slot.itemId, ctx.maxStack, ctx.blockItemSlots).map((m) => ({
      type: "block_inventory.move_item",
      payload: { from, to: { area: "block", slot: m.slot }, count: m.count },
    }));
  }
  const targetArea: InventoryArea = from.area === "hotbar" ? "main" : "hotbar";
  const targetSlots = targetArea === "main" ? ctx.inventory.mainSlots : ctx.inventory.hotbarSlots;
  return planDirectMoves(slot.count, slot.itemId, ctx.maxStack, targetSlots).map((m) => ({
    type: "inventory.move_item",
    payload: { from, to: { area: targetArea, slot: m.slot }, count: m.count },
  }));
}
```

`src/shared/itemMove/blockSlotPlan.ts`:

```ts
import type { SlotData } from "@/bridge/contract/payloadTypes";
import { planDirectMoves } from "./planDirectMoves";
import type { PlannedAction } from "./plannedAction";

// ブロックスロット操作の判定材料。mainSlots は Shift 配分の宛先（uGUI はサブ→メインのみでホットバー除外）
// Inputs for block-slot decisions; mainSlots is the Shift target (uGUI moves sub→main only, hotbar excluded)
export type BlockSlotContext = {
  grabCount: number;
  maxStack: number | undefined;
  mainSlots: SlotData[];
};

const grabRef = { area: "grab", slot: 0 } as const;
const blockRef = (slot: number) => ({ area: "block", slot }) as const;

// 左クリック: Shift(空手+中身あり)は main へ配分 / grab保持なら全量置き / 中身ありなら全量掴み
// Left click: Shift (empty-handed, filled) allocates into main / place all while holding grab / pick the whole stack
export function planBlockLeftClick(index: number, slot: SlotData, shiftKey: boolean, ctx: BlockSlotContext): PlannedAction[] {
  if (shiftKey && ctx.grabCount === 0 && slot.count > 0) {
    return planDirectMoves(slot.count, slot.itemId, ctx.maxStack, ctx.mainSlots).map((m) => ({
      type: "block_inventory.move_item",
      payload: { from: blockRef(index), to: { area: "main", slot: m.slot }, count: m.count },
    }));
  }
  if (ctx.grabCount > 0) {
    return [{ type: "block_inventory.move_item", payload: { from: grabRef, to: blockRef(index), count: ctx.grabCount } }];
  }
  if (slot.itemId > 0) {
    return [{ type: "block_inventory.move_item", payload: { from: blockRef(index), to: grabRef, count: slot.count } }];
  }
  return [];
}

// 右クリック: grab保持なら1個置き / 空手で2個以上なら半分(切り捨て)を grab へ
// Right click: place one while holding grab / grab half (floor) of 2+ items empty-handed
// 注意: ホストに block_inventory.split が無いためここだけ client 計算（player 側 inventory.split と唯一異なる点）
// Note: no block_inventory.split exists on the host, so only this path computes client-side (the sole divergence from inventory.split)
export function planBlockRightClick(index: number, slot: SlotData, grabCount: number): PlannedAction[] {
  if (grabCount > 0) {
    return [{ type: "block_inventory.move_item", payload: { from: grabRef, to: blockRef(index), count: 1 } }];
  }
  const half = Math.floor(slot.count / 2);
  if (slot.itemId > 0 && half > 0) {
    return [{ type: "block_inventory.move_item", payload: { from: blockRef(index), to: grabRef, count: half } }];
  }
  return [];
}

// ダブルクリック: 収集先（grab かクリックスロットか）はホストが自身の grab 状態で決める
// Double click: the host decides the target (grab vs clicked slot) from its own grab state
export function planBlockDoubleClick(index: number): PlannedAction[] {
  return [{ type: "block_inventory.collect", payload: { slot: blockRef(index) } }];
}
```

`src/shared/itemMove/index.ts`:

```ts
export type { PlannedAction } from "./plannedAction";
export { dispatchPlanned } from "./dispatchPlanned";
export { planDirectMoves, type PlannedMove } from "./planDirectMoves";
export { GRAB, planPlayerLeftClick, planPlayerRightClick, planPlayerDoubleClick, type PlayerSlotContext } from "./playerSlotPlan";
export { planBlockLeftClick, planBlockRightClick, planBlockDoubleClick, type BlockSlotContext } from "./blockSlotPlan";
```

- [ ] **Step 6: 旧 inventoryLogic を削除し import を向け替え**

- `src/features/inventory/inventoryLogic.ts` と `src/features/inventory/inventoryLogic.test.ts` を削除
- `src/features/inventory/slotActions.ts:3` を `import { planDirectMoves } from "@/shared/itemMove";` に変更
- `src/features/blockInventory/blockLogic.ts:4` を `import { planDirectMoves } from "@/shared/itemMove";` に変更（feature間直importの解消）

- [ ] **Step 7: テスト・型・e2e スモーク**

Run: `pnpm exec vitest run src/shared/itemMove/` → PASS
Run: `pnpm test && pnpm build` → PASS
Run: `pnpm exec playwright test --config e2e/playwright.config.ts blockInventoryGestures` → PASS

- [ ] **Step 8: Commit**

```bash
git add src/shared/itemMove/ src/features/
git commit -m "refactor(webui): スロット移動計画をshared/itemMoveへ集約しfeature間直importを解消"
```

---

### Task 6: プレイヤー側スロット操作を共有プランナへ切替

`slotActions.ts` を「topic を読んで文脈を組み、プランナの結果を dispatch する」薄いアダプタにする。`inventory.split` / block payload 組み立ての知識が feature から消える。

**Files:**
- Modify: `src/features/inventory/slotActions.ts`（全面書き換え）

**Interfaces:**
- Consumes: Task 5 の `planPlayerLeftClick` / `planPlayerRightClick` / `planPlayerDoubleClick` / `dispatchPlanned`
- Produces: `createSlotActions(inventory, itemMaster): SlotActions`（既存シグネチャ不変。InventoryPanel/HotbarPanel は無変更）

- [ ] **Step 1: slotActions.ts を書き換え**

全体を以下に置換:

```ts
import { readTopic, Topics } from "@/bridge";
import type { ItemMasterEntry, PlayerInventoryData, SlotData, SlotRef } from "@/bridge/contract/payloadTypes";
import {
  dispatchPlanned,
  planPlayerDoubleClick,
  planPlayerLeftClick,
  planPlayerRightClick,
  type PlayerSlotContext,
} from "@/shared/itemMove";

// プレイヤースロット共通のクリック操作。InventoryPanel と HotbarPanel が共用する
// Player-slot click interactions shared by InventoryPanel and HotbarPanel
export type SlotActions = {
  onLeftDown: (ref: SlotRef, slot: SlotData, shiftKey: boolean) => void;
  onRightDown: (ref: SlotRef, slot: SlotData) => void;
  onDoubleClick: (ref: SlotRef) => void;
};

// 判定は shared/itemMove の純関数プランナに委譲し、ここは topic 読み出しと送信の配線だけを持つ
// Decisions live in the shared/itemMove pure planners; this file only wires topic reads to dispatch
export function createSlotActions(
  inventory: PlayerInventoryData,
  itemMaster: Map<number, ItemMasterEntry> | null,
): SlotActions {
  const onLeftDown = (ref: SlotRef, slot: SlotData, shiftKey: boolean) => {
    // block 開閉は event 時点の最新値を readTopic で読む（キー入力リスナーと同じ規約）
    // Read the block open state at event time via readTopic (same contract as the keydown listener)
    const block = readTopic(Topics.blockInventory);
    const ctx: PlayerSlotContext = {
      inventory,
      // マスタ未ロード時は maxStack 不明として planDirectMoves が空スロットのみ使う
      // With the master unloaded, maxStack is unknown and planDirectMoves falls back to empty slots
      maxStack: itemMaster?.get(slot.itemId)?.maxStack,
      blockItemSlots: block?.open ? block.itemSlots : null,
    };
    dispatchPlanned(planPlayerLeftClick(ref, slot, shiftKey, ctx));
  };

  const onRightDown = (ref: SlotRef, slot: SlotData) => {
    dispatchPlanned(planPlayerRightClick(ref, slot, inventory.grab.count));
  };

  const onDoubleClick = (ref: SlotRef) => {
    dispatchPlanned(planPlayerDoubleClick(ref));
  };

  return { onLeftDown, onRightDown, onDoubleClick };
}
```

- [ ] **Step 2: 回帰確認（プレイヤー操作の e2e が全て既存のまま通ること）**

Run: `pnpm build && pnpm test` → PASS
Run: `pnpm exec playwright test --config e2e/playwright.config.ts inventory hotbar blockInventoryGestures` → PASS

- [ ] **Step 3: Commit**

```bash
git add src/features/inventory/slotActions.ts
git commit -m "refactor(webui): プレイヤースロット操作をshared/itemMoveプランナ経由に切替"
```

---

### Task 7: ブロック側切替＋useBlockSlotGestures＋MachineSection フル操作＋レジストリ分離

ジェスチャ配線をグリッド形状から分離し、MachineSection に右クリック/Shift/ダブルクリックを配線する（Web 独自の操作パリティ欠落の修正）。blockLogic.ts は純関数が shared へ移った結果レジストリだけになるので `blockComponentRegistry.ts` に改名し、logic テストから `vi.mock` を追放する。

**Files:**
- Create: `src/features/blockInventory/useBlockSlotGestures.ts`
- Create: `src/features/blockInventory/blockComponentRegistry.ts`（旧 blockLogic.ts のレジストリ部）
- Create: `src/features/blockInventory/blockComponentRegistry.test.ts`（旧 blockLogic.test.ts のレジストリ部）
- Create: `e2e/tests/block/machineGestures.spec.ts`
- Modify: `src/features/blockInventory/BlockItemGrid.tsx`
- Modify: `src/features/blockInventory/details/MachineSection.tsx`
- Modify: `src/features/blockInventory/BlockInventoryPanel.tsx:5`（import 向け替え）
- Delete: `src/features/blockInventory/blockLogic.ts` / `src/features/blockInventory/blockLogic.test.ts`

**Interfaces:**
- Consumes: Task 5 の `planBlockLeftClick` / `planBlockRightClick` / `planBlockDoubleClick` / `dispatchPlanned`、既存 `useBlockInteraction`
- Produces:
  - `useBlockSlotGestures(): { onLeftDown(index, slot, shiftKey), onRightDown(index, slot), onDoubleClick(index) }`
  - `resolveBlockComponent(blockType): BlockInventoryComponent` と `blockComponents`（旧 blockLogic と同一シグネチャ、ファイル移動のみ）

- [ ] **Step 1: 失敗する e2e（機械スロットのフル操作）を書く**

`e2e/tests/block/machineGestures.spec.ts`:

```ts
import { test, expect } from "@playwright/test";
import { payloadsOf } from "../../support/actions";
import { setBlock } from "../../support/mockControl";

// 機械の入出力スロットにもチェストと同じフル操作（右クリ/Shift/収集）が効くことを検証する
// Assert machine in/out slots support the full gesture set (right-click/Shift/collect) like chests
test.beforeEach(async ({ page }) => {
  await setBlock(page, "machine");
  await page.goto("/");
  await expect(page.getByTestId("machine-section")).toBeVisible();
});
test.afterEach(async ({ page }) => {
  await setBlock(page, "closed");
});

test("機械入力スロットの空手右クリックで半分(切り捨て)を grab へ拾う", async ({ page }) => {
  // input slot0 = itemId3×5 → 半分は2
  // input slot0 = itemId3 x5, so half floors to 2
  await page.getByTestId("machine-input-slots").locator("> div").first().click({ button: "right" });
  await expect
    .poll(() => payloadsOf(page, "block_inventory.move_item"))
    .toContainEqual({ from: { area: "block", slot: 0 }, to: { area: "grab", slot: 0 }, count: 2 });
});

test("機械出力スロットのダブルクリックで collect を送る", async ({ page }) => {
  // 出力は slotLayout.input=2 の直後 → 統合 index 2
  // Output starts right after slotLayout.input=2, i.e. combined index 2
  await page.getByTestId("machine-output-slots").locator("> div").first().dblclick();
  await expect
    .poll(() => payloadsOf(page, "block_inventory.collect"))
    .toContainEqual({ slot: { area: "block", slot: 2 } });
});

test("機械入力スロットの Shift+クリックで main へ配分移動する", async ({ page }) => {
  // main に itemId3 のスタックが無いため最初の空きスロット(index3)へ全量5
  // main holds no itemId3 stack, so all 5 go to the first empty slot (index 3)
  await page.getByTestId("machine-input-slots").locator("> div").first().click({ modifiers: ["Shift"] });
  await expect
    .poll(() => payloadsOf(page, "block_inventory.move_item"))
    .toContainEqual({ from: { area: "block", slot: 0 }, to: { area: "main", slot: 3 }, count: 5 });
});
```

Run: `pnpm exec playwright test --config e2e/playwright.config.ts machineGestures`
Expected: FAIL（3件とも。現行 MachineSection は左クリックしか配線していない）

- [ ] **Step 2: useBlockSlotGestures を実装**

`src/features/blockInventory/useBlockSlotGestures.ts`:

```ts
import { readTopic, Topics } from "@/bridge";
import type { SlotData } from "@/bridge/contract/payloadTypes";
import {
  dispatchPlanned,
  planBlockDoubleClick,
  planBlockLeftClick,
  planBlockRightClick,
} from "@/shared/itemMove";
import { useBlockInteraction } from "./blockInteractionContext";

export type BlockSlotGestures = {
  onLeftDown: (index: number, slot: SlotData, shiftKey: boolean) => void;
  onRightDown: (index: number, slot: SlotData) => void;
  onDoubleClick: (index: number) => void;
};

// ブロックスロット共通のジェスチャ配線。グリッド形状に依存しないため任意レイアウト（機械の分割グリッド等）で使える
// Shared gesture wiring for block slots; layout-agnostic so split grids like machines reuse it
export function useBlockSlotGestures(): BlockSlotGestures {
  const { grabCount, resolveMaxStack } = useBlockInteraction();

  const onLeftDown = (index: number, slot: SlotData, shiftKey: boolean) => {
    // 最新の main スロットは event 時点で readTopic から読む（購読による再レンダー増を避ける）
    // Read the latest main slots via readTopic at event time (avoids extra re-renders from subscribing)
    const inventory = readTopic(Topics.inventory);
    if (!inventory) return;
    dispatchPlanned(
      planBlockLeftClick(index, slot, shiftKey, {
        grabCount,
        maxStack: resolveMaxStack(slot.itemId),
        mainSlots: inventory.mainSlots,
      }),
    );
  };

  const onRightDown = (index: number, slot: SlotData) => {
    dispatchPlanned(planBlockRightClick(index, slot, grabCount));
  };

  const onDoubleClick = (index: number) => {
    dispatchPlanned(planBlockDoubleClick(index));
  };

  return { onLeftDown, onRightDown, onDoubleClick };
}
```

- [ ] **Step 3: BlockItemGrid をフックへ移行**

`src/features/blockInventory/BlockItemGrid.tsx` 全体を以下に置換:

```tsx
import { ItemSlot, SlotGrid } from "@/shared/ui";
import type { SlotData } from "@/bridge/contract/payloadTypes";
import { useBlockInteraction } from "./blockInteractionContext";
import { useBlockSlotGestures } from "./useBlockSlotGestures";

// itemSlots を 9 幅グリッドで描画し grab/move_item/collect と連動する共通部品
// 9-wide grid of a block's itemSlots, wired to grab via move_item/collect
export default function BlockItemGrid({ itemSlots, testId }: { itemSlots: SlotData[]; testId: string }) {
  const { resolveName } = useBlockInteraction();
  // ジェスチャ配線は useBlockSlotGestures に共通化（MachineSection の分割グリッドと同一挙動）
  // Gesture wiring is shared via useBlockSlotGestures (identical to MachineSection's split grids)
  const gestures = useBlockSlotGestures();

  return (
    <SlotGrid testId={testId}>
      {itemSlots.map((slot, index) => (
        <ItemSlot
          key={index}
          itemId={slot.itemId}
          count={slot.count}
          name={resolveName(slot.itemId)}
          onLeftDown={(shiftKey) => gestures.onLeftDown(index, slot, shiftKey)}
          onRightDown={() => gestures.onRightDown(index, slot)}
          onDoubleClick={() => gestures.onDoubleClick(index)}
        />
      ))}
    </SlotGrid>
  );
}
```

- [ ] **Step 4: MachineSection にフル操作を配線**

`src/features/blockInventory/details/MachineSection.tsx` の import と `slotAt` を変更。import 部:

```tsx
import { Group, Stack, Text } from "@mantine/core";
import type { BlockInventoryOpen } from "@/bridge/contract/payloadTypes";
import { ItemSlot, SlotGrid, ProgressArrow, FluidSlot } from "@/shared/ui";
import { useBlockInteraction } from "../blockInteractionContext";
import { useBlockSlotGestures } from "../useBlockSlotGestures";
import { computePowerRate, splitSlotIndices } from "./detailLogic";
```

（`dispatchAction` と `blockSlotClickPayload` の import は削除）

コンポーネント冒頭〜 `slotAt` を以下に置換:

```tsx
export default function MachineSection({ data }: { data: BlockInventoryOpen }) {
  const { resolveName } = useBlockInteraction();
  // ジェスチャ配線は BlockItemGrid と共通。分割グリッドでも右クリ/Shift/収集がフルに効く
  // Gesture wiring shared with BlockItemGrid; split grids get the full right-click/Shift/collect set
  const gestures = useBlockSlotGestures();
  if (!data.machine) return null;
  const { input, output, module } = splitSlotIndices(data.machine.slotLayout, data.itemSlots.length);
  const powerRate = computePowerRate(data.machine.currentPower, data.machine.requestPower);
  const lacking = powerRate < 1;

  const slotAt = (i: number) => {
    const slot = data.itemSlots[i];
    return (
      <ItemSlot
        key={i}
        itemId={slot.itemId}
        count={slot.count}
        name={resolveName(slot.itemId)}
        onLeftDown={(shiftKey) => gestures.onLeftDown(i, slot, shiftKey)}
        onRightDown={() => gestures.onRightDown(i, slot)}
        onDoubleClick={() => gestures.onDoubleClick(i)}
      />
    );
  };
```

（return 以降の JSX は不変。Hooks は早期 return より前に呼ぶこと — `useBlockSlotGestures()` は `if (!data.machine)` より上に置く）

- [ ] **Step 5: blockLogic をレジストリ専用ファイルへ改名**

`src/features/blockInventory/blockComponentRegistry.ts` を新規作成し、旧 `blockLogic.ts` の**レジストリ部分のみ**（`BlockInventoryComponent` 型・`blockComponents`・`resolveBlockComponent` と8つの view import、73〜94行相当）を移す。純関数（pickUpPayload 等）は Task 5 で shared に移植済みのため持ち込まない。

`src/features/blockInventory/blockComponentRegistry.test.ts` を新規作成し、旧 `blockLogic.test.ts` から**レジストリ関連の describe のみ**（`resolveBlockComponent` と `TankInventory` の2ブロック、および冒頭の `vi.mock("@/bridge/transport/webSocketClient", ...)`）を移す。純関数の describe（pickUpPayload/placePayload/blockSlotClickPayload/blockSlotRightClickPayload/blockShiftMovePayloads）は Task 5 の `blockSlotPlan.test.ts` に移植済みのため削除する。

その後:
- `src/features/blockInventory/BlockInventoryPanel.tsx:5` を `import { resolveBlockComponent } from "./blockComponentRegistry";` に変更
- `src/features/blockInventory/blockLogic.ts` と `src/features/blockInventory/blockLogic.test.ts` を削除
- 確認: `grep -rn "blockLogic" src/ e2e/` が0件

- [ ] **Step 6: 全テスト・e2e**

Run: `pnpm build && pnpm test` → PASS
Run: `pnpm exec playwright test --config e2e/playwright.config.ts machineGestures blockInventoryGestures blockInventory blockDetails` → PASS（machineGestures 3件が green に転じる）

- [ ] **Step 7: Commit**

```bash
git add src/features/blockInventory/ e2e/tests/block/machineGestures.spec.ts
git commit -m "fix(webui): 機械スロットに右クリック/Shift/収集を配線しジェスチャをuseBlockSlotGesturesへ共通化"
```

---

### Task 8: activeLayer/uiScreenRouting を shared/uiState へ移設＋UiStateNames＋useGameLayerKeydown

features → app の依存逆転を解消し、二重実装の keydown ゲート（HotbarPanel は入力欄ガードあり・App は無し）を共有フックに統一する。

**Files:**
- Create: `src/shared/uiState/useGameLayerKeydown.ts`
- Create: `src/shared/uiState/index.ts`
- Move: `src/app/activeLayer.ts` → `src/shared/uiState/activeLayer.ts`（test も）
- Move: `src/app/uiScreenRouting.ts` → `src/shared/uiState/uiScreenRouting.ts`（test も）
- Modify: `src/bridge/transport/protocol.ts`（UiStateNames 追加）
- Modify: `src/app/App.tsx` / `src/features/inventory/HotbarPanel/index.tsx` / `src/features/blockInventory/BlockInventoryPanel.tsx` / `e2e/mock-host/wsHandler.ts`

**Interfaces:**
- Produces:
  - `@/shared/uiState` barrel: `deriveActiveLayer` / `readActiveLayer` / `ActiveLayer` / `screenForUiState` / `UiScreen` / `useGameLayerKeydown(handler)`
  - `UiStateNames = { gameScreen: "GameScreen", playerInventory: "PlayerInventory", subInventory: "SubInventory", researchTree: "ResearchTree" } as const`（protocol.ts）

- [ ] **Step 1: protocol.ts に UiStateNames を追加**

`Topics` 定義の直後に追加:

```ts
// C# UIStateEnum 由来の state 名。文字列リテラルの散在を防ぐ
// State names from the C# UIStateEnum; prevents scattered string literals
export const UiStateNames = {
  gameScreen: "GameScreen",
  playerInventory: "PlayerInventory",
  subInventory: "SubInventory",
  researchTree: "ResearchTree",
} as const;
```

`src/bridge/index.ts` の Topics export 行に追加: `export { Topics, UiStateNames } from "./transport/protocol";`

- [ ] **Step 2: ファイル移動と UiStateNames 適用**

```bash
mkdir -p src/shared/uiState
git mv src/app/activeLayer.ts src/app/activeLayer.test.ts src/shared/uiState/
git mv src/app/uiScreenRouting.ts src/app/uiScreenRouting.test.ts src/shared/uiState/
```

`src/shared/uiState/uiScreenRouting.ts` を UiStateNames 参照に書き換え:

```ts
import { UiStateNames } from "@/bridge";

// ui_state.current の state 名 → Web が描画する画面。App.tsx ルーティングの単一の正
// Maps ui_state.current's state name to the web screen; single source for App.tsx routing
export type UiScreen = "none" | "playerInventory" | "subInventory" | "researchTree";

export function screenForUiState(state: string | null): UiScreen {
  if (state === UiStateNames.playerInventory) return "playerInventory";
  if (state === UiStateNames.subInventory) return "subInventory";
  if (state === UiStateNames.researchTree) return "researchTree";
  // GameScreen・未対応state・未受信はパネル無し（前方互換: 未知state名も安全側に倒す)
  // GameScreen, unsupported states and pre-snapshot are panel-less (forward-compat: unknown names fail safe)
  return "none";
}
```

`src/shared/uiState/activeLayer.ts` の26行目を `researchOpen: uiState?.state === UiStateNames.researchTree,` に変更し、import を `import { readTopic, Topics, UiStateNames } from "@/bridge";` にする。

- [ ] **Step 3: useGameLayerKeydown と barrel を作成**

`src/shared/uiState/useGameLayerKeydown.ts`:

```ts
import { useEffect, useRef } from "react";
import { readActiveLayer } from "./activeLayer";

// game レイヤー時のみ発火するグローバル keydown。入力欄フォーカス中はゲーム操作を奪わない
// Global keydown firing only at the game layer; never hijacks typing while an input is focused
export function useGameLayerKeydown(handler: (e: KeyboardEvent) => void): void {
  // リスナーは1回だけ張り、最新の handler は ref 経由で呼ぶ
  // Attach the listener once and call the latest handler through a ref
  const handlerRef = useRef(handler);
  handlerRef.current = handler;

  useEffect(() => {
    const onKeyDown = (e: KeyboardEvent) => {
      const tag = document.activeElement?.tagName;
      if (tag === "INPUT" || tag === "TEXTAREA") return;
      if (readActiveLayer() !== "game") return;
      handlerRef.current(e);
    };
    window.addEventListener("keydown", onKeyDown);
    return () => window.removeEventListener("keydown", onKeyDown);
  }, []);
}
```

`src/shared/uiState/index.ts`:

```ts
export { deriveActiveLayer, readActiveLayer, type ActiveLayer } from "./activeLayer";
export { screenForUiState, type UiScreen } from "./uiScreenRouting";
export { useGameLayerKeydown } from "./useGameLayerKeydown";
```

- [ ] **Step 4: 呼び出し側を更新**

`src/features/inventory/HotbarPanel/index.tsx`:
- import の `@/app/activeLayer` / `@/app/uiScreenRouting` 2行を `import { readActiveLayer, screenForUiState, useGameLayerKeydown } from "@/shared/uiState";` に置換
- 24〜44行の `useEffect` キーリスナー全体を以下に置換（ホイール側の `readActiveLayer` ガードは不変）:

```tsx
  // 1-9 キーでホットバー選択。ゲートは共有フックが担い、最新値は readTopic で読む
  // Keys 1-9 select a hotbar slot; the shared hook gates it and the latest value comes via readTopic
  useGameLayerKeydown((e) => {
    const latest = readTopic(Topics.inventory);
    if (!latest) return;
    const index = keyToHotbarIndex(e.key);
    if (index === null || index >= latest.hotbarSlots.length) return;
    // 実際に選択が変わるときだけ送信する（uGUI 同様）
    // Dispatch only when the selection actually changes, matching uGUI
    if (index === latest.selectedHotbar) return;
    void dispatchAction("inventory.select_hotbar", { index });
  });
```

`src/app/App.tsx`:
- `import { readActiveLayer } from "./activeLayer";` と `import { screenForUiState } from "./uiScreenRouting";` を `import { screenForUiState, useGameLayerKeydown } from "@/shared/uiState";` に置換
- 33〜41行の Esc `useEffect` を以下に置換（入力欄ガードが App 側にも効くようになる＝二重実装の乖離解消）:

```tsx
  // Esc でアイテム選択を解除する。modal 等のオーバーレイは自前で Esc を処理するため game レイヤーのみ
  // Esc clears item selection; overlays like the modal handle Esc themselves, so only at the game layer
  useGameLayerKeydown((e) => {
    if (e.key !== "Escape") return;
    useUiStore.getState().clearSelectedItem();
  });
```

`src/features/blockInventory/BlockInventoryPanel.tsx` の CloseButton onClick を `void dispatchAction("ui_state.request", { state: UiStateNames.gameScreen });` にし、import に `UiStateNames` を追加。

`e2e/mock-host/wsHandler.ts` の `ui_state.request` 分岐（135・138・139・142行の生文字列）を `UiStateNames.gameScreen` / `UiStateNames.playerInventory` 参照に変更し、import に `UiStateNames` を追加。

- [ ] **Step 5: テスト移動の確認と回帰**

Run: `pnpm exec vitest run src/shared/uiState/` → PASS（移設した activeLayer/uiScreenRouting テストが通る）
Run: `pnpm build && pnpm test` → PASS
Run: `pnpm test:e2e` → PASS（特に uiState / hotbar / modal）
確認: `grep -rn "@/app/" src/features/` が `@/app/uiStore` のみになる（Task 9 で解消）。

- [ ] **Step 6: Commit**

```bash
git add src/shared/uiState/ src/app/ src/features/ src/bridge/ e2e/mock-host/wsHandler.ts
git commit -m "refactor(webui): activeLayer/uiScreenRoutingをshared/uiStateへ移設しkeydownゲートをuseGameLayerKeydownに統一"
```

---

### Task 9: uiStore を features/recipe/selectionStore へ移動＋ItemListPanel の配置修正＋barrel整理

features → app 依存の最後の1本（uiStore）を切る。selectedItemId は実態が recipe feature ローカル状態。

**Files:**
- Create: `src/features/recipe/selectionStore.ts`
- Move: `src/features/recipe/views/ItemListPanel.tsx` → `src/features/recipe/ItemListPanel.tsx`
- Modify: `src/features/recipe/index.ts` / `src/features/recipe/RecipeViewer.tsx` / `src/app/App.tsx` / `src/app/DebugActionButton.tsx`
- Delete: `src/app/uiStore.ts`

**Interfaces:**
- Produces: `useItemSelectionStore`（feature内）、`clearSelectedItem(): void`（barrel経由でAppが使用）

- [ ] **Step 1: selectionStore を作成**

`src/features/recipe/selectionStore.ts`:

```ts
import { create } from "zustand";

// レシピビューアのアイテム選択状態（recipe feature ローカル。remote topic データは入れない）
// Item-selection state local to the recipe feature (never holds remote topic data)
type ItemSelectionState = {
  selectedItemId: number | null;
  setSelectedItem: (itemId: number) => void;
  clearSelectedItem: () => void;
};

export const useItemSelectionStore = create<ItemSelectionState>((set) => ({
  selectedItemId: null,
  setSelectedItem: (itemId) => set({ selectedItemId: itemId }),
  clearSelectedItem: () => set({ selectedItemId: null }),
}));

// フック外（App の Esc ハンドラ等）から選択を解除する命令的アクセサ
// Imperative accessor to clear the selection outside hooks (e.g. the App Esc handler)
export function clearSelectedItem(): void {
  useItemSelectionStore.getState().clearSelectedItem();
}
```

- [ ] **Step 2: ItemListPanel を移動し参照を更新**

```bash
git mv src/features/recipe/views/ItemListPanel.tsx src/features/recipe/ItemListPanel.tsx
```

- `src/features/recipe/ItemListPanel.tsx`: `import { useUiStore } from "@/app/uiStore";` → `import { useItemSelectionStore } from "./selectionStore";`、`useUiStore(...)` 2箇所を `useItemSelectionStore(...)` に変更
- `src/features/recipe/RecipeViewer.tsx`: 同様に `./selectionStore` の `useItemSelectionStore` へ変更
- `src/features/recipe/index.ts`:

```ts
export { default as RecipeViewer } from "./RecipeViewer";
export { default as ItemListPanel } from "./ItemListPanel";
export { clearSelectedItem } from "./selectionStore";
```

- [ ] **Step 3: App と DebugActionButton の依存を整理**

- `src/app/App.tsx`: `import { useUiStore } from "./uiStore";` を削除し、既存の `@/features/recipe` import を `import { RecipeViewer, ItemListPanel, clearSelectedItem } from "@/features/recipe";` に変更。Esc ハンドラ本体を `clearSelectedItem();` に変更
- `src/app/uiStore.ts` を削除
- `src/app/DebugActionButton.tsx`: `import { emitToast } from "@/features/toast/toastStore";` → `import { emitToast } from "@/features/toast";`、`import { dispatchAction } from "@/bridge/transport/actions";` → `import { dispatchAction } from "@/bridge";`

- [ ] **Step 4: 依存方向の完了判定と回帰**

確認: `grep -rn "@/app/" src/features/` → 0件（features→app 依存の全廃）
Run: `pnpm build && pnpm test` → PASS
Run: `pnpm exec playwright test --config e2e/playwright.config.ts recipe uiState` → PASS（Esc選択解除・アイテム選択の回帰）

- [ ] **Step 5: Commit**

```bash
git add -A src/app/ src/features/recipe/
git commit -m "refactor(webui): selectedItemIdをfeatures/recipe/selectionStoreへ移しfeatures→app依存を全廃"
```

---

### Task 10: buildOwnedCounts を shared へ一本化

recipe（`count>0` ガード）と research（`itemId<=0` ガード）で二重定義されていた所持数集計を統一する。

**Files:**
- Create: `src/shared/ownedCounts.ts`
- Create: `src/shared/ownedCounts.test.ts`
- Modify: `src/features/recipe/craftLogic.ts`（buildOwnedCounts 削除）/ `src/features/recipe/craftLogic.test.ts`（該当 describe 削除）
- Modify: `src/features/recipe/views/RecipeContent.tsx`
- Modify: `src/features/research/researchLogic.ts`（buildOwnedCounts 削除）/ `src/features/research/researchLogic.test.ts`（該当 describe 削除）
- Modify: `src/features/research/ResearchTreePanel.tsx:5`

**Interfaces:**
- Produces: `buildOwnedCounts(slots: SlotData[]): Map<number, number>`（空スロット= itemId<=0 または count<=0 を除外）

- [ ] **Step 1: 失敗するテストを書く**

`src/shared/ownedCounts.test.ts`:

```ts
import { describe, it, expect } from "vitest";
import { buildOwnedCounts } from "./ownedCounts";

const slot = (itemId: number, count: number) => ({ itemId, count });

describe("buildOwnedCounts", () => {
  it("同一 itemId を跨スロットで合算する", () => {
    const owned = buildOwnedCounts([slot(1, 10), slot(2, 3), slot(1, 5)]);
    expect(owned.get(1)).toBe(15);
    expect(owned.get(2)).toBe(3);
  });
  it("空スロット(itemId=0)は無視する", () => {
    expect(buildOwnedCounts([slot(0, 0)]).size).toBe(0);
  });
  it("count<=0 のスロットはエントリを作らない（旧recipe実装と同挙動）", () => {
    expect(buildOwnedCounts([slot(5, 0)]).has(5)).toBe(false);
  });
});
```

Run: `pnpm exec vitest run src/shared/ownedCounts.test.ts` → FAIL（モジュール未作成）

- [ ] **Step 2: 実装**

`src/shared/ownedCounts.ts`:

```ts
import type { SlotData } from "@/bridge/contract/payloadTypes";

// スロット列から itemId 別の所持数を集計する。空スロット・0個は除外（recipe/research 共用）
// Tally owned counts per itemId from slots, skipping empties and zero counts (shared by recipe/research)
export function buildOwnedCounts(slots: SlotData[]): Map<number, number> {
  const owned = new Map<number, number>();
  for (const slot of slots) {
    if (slot.itemId <= 0 || slot.count <= 0) continue;
    owned.set(slot.itemId, (owned.get(slot.itemId) ?? 0) + slot.count);
  }
  return owned;
}
```

- [ ] **Step 3: 両 feature を差し替え**

- `src/features/recipe/craftLogic.ts`: `buildOwnedCounts`（50〜60行）と `PlayerInventoryData` import を削除
- `src/features/recipe/views/RecipeContent.tsx`: import に `import { buildOwnedCounts } from "@/shared/ownedCounts";` を追加（craftLogic からの `buildOwnedCounts` import は削除）し、40行目を:

```tsx
  // サーバーの OneClickCraft は main+hotbar のみ参照するため、grab は所持数に含めない
  // The server's OneClickCraft only consults main+hotbar, so grab is excluded from the tally
  const counts = useMemo(() => buildOwnedCounts([...inventory.mainSlots, ...inventory.hotbarSlots]), [inventory]);
```

- `src/features/research/researchLogic.ts`: `buildOwnedCounts`（37〜46行）を削除（`SlotData` import が不要になれば併せて削除）
- `src/features/research/ResearchTreePanel.tsx:5`: `buildOwnedCounts` を `researchLogic` からではなく `import { buildOwnedCounts } from "@/shared/ownedCounts";` で取得（`computeCanvasBounds, lineBetween` の import は残す）
- `craftLogic.test.ts` / `researchLogic.test.ts` から `buildOwnedCounts` の describe を削除（挙動は shared のテストがカバー）

- [ ] **Step 4: 回帰**

Run: `pnpm build && pnpm test` → PASS
Run: `pnpm exec playwright test --config e2e/playwright.config.ts recipe research` → PASS

- [ ] **Step 5: Commit**

```bash
git add src/shared/ownedCounts.ts src/shared/ownedCounts.test.ts src/features/
git commit -m "refactor(webui): 所持数集計buildOwnedCountsをsharedへ一本化しrecipe/researchの定義乖離を解消"
```

---

### Task 11: FluidSlotRow・PowerRateText 抽出と小修正

JSX コピペ3系統（流体行・電力率テキスト）の抽出と、レビューで確定した小バグ3件（fuelRatio の clamp01 未使用・RecipeContent のタブフォールバック時 index 持ち越し・MinerSection の key 衝突）をまとめて解消する。

**Files:**
- Create: `src/shared/ui/FluidSlotRow/index.tsx`
- Create: `src/features/blockInventory/details/PowerRateText.tsx`
- Modify: `src/shared/ui/index.ts`
- Modify: `src/features/blockInventory/views/TankInventory.tsx` / `views/GenericBlockInventory.tsx` / `details/MachineSection.tsx` / `details/MinerSection.tsx` / `details/detailLogic.ts` / `src/features/recipe/views/RecipeContent.tsx`

**Interfaces:**
- Produces: `FluidSlotRow({ fluids, progress?, testId })`（fluids 空なら null、progress は非null時のみ矢印描画＝null時非表示に統一）、`PowerRateText({ currentPower, requestPower, testId })`

- [ ] **Step 1: FluidSlotRow を作成**

`src/shared/ui/FluidSlotRow/index.tsx`:

```tsx
import { Group } from "@mantine/core";
import type { FluidSlotData } from "@/bridge/contract/payloadTypes";
import FluidSlot from "../FluidSlot";
import ProgressArrow from "../ProgressArrow";

type Props = {
  fluids: FluidSlotData[];
  // 進捗矢印は progress が数値のときだけ描画する（null/undefined は非表示で統一）
  // Draw the progress arrow only for a numeric progress (null/undefined uniformly hides it)
  progress?: number | null;
  testId: string;
};

// 流体スロット横並び＋任意の進捗矢印。Tank/Generic/Machine の3重複を置き換える共通部品
// Fluid slots in a row plus an optional progress arrow; replaces the Tank/Generic/Machine triplication
export default function FluidSlotRow({ fluids, progress, testId }: Props) {
  if (fluids.length === 0) return null;
  return (
    <Group data-testid={testId} gap="xs" align="center">
      {fluids.map((fluid, i) => (
        <FluidSlot key={i} fluid={fluid} />
      ))}
      {progress != null ? <ProgressArrow value={progress} /> : null}
    </Group>
  );
}
```

`src/shared/ui/index.ts` に追加: `export { default as FluidSlotRow } from "./FluidSlotRow";`

- [ ] **Step 2: 3箇所を FluidSlotRow へ置換**

`src/features/blockInventory/views/TankInventory.tsx` 全体:

```tsx
import type { BlockInventoryOpen } from "@/bridge/contract/payloadTypes";
import { FluidSlotRow } from "@/shared/ui";

// Tank UI: uGUI 流体タンク同様 fluidSlots を列展開＋進捗矢印
// Tank UI: mirrors uGUI fluid tank; fluidSlots row plus a progress arrow
export default function TankInventory({ data }: { data: BlockInventoryOpen }) {
  return <FluidSlotRow fluids={data.fluidSlots} progress={data.progress} testId="tank-body" />;
}
```

`src/features/blockInventory/views/GenericBlockInventory.tsx` の流体ブロック（17〜25行）を:

```tsx
      {/* 流体スロットは共通の FluidSlotRow（空なら非描画） */}
      {/* Fluid slots via the shared FluidSlotRow (renders nothing when empty) */}
      <FluidSlotRow fluids={data.fluidSlots} progress={data.progress} testId="generic-block-fluids" />
```

に置換し、import を `import { FluidSlotRow } from "@/shared/ui";`（`FluidSlot, ProgressArrow, Group` の不要 import は削除）。

`src/features/blockInventory/details/MachineSection.tsx` の fluid 部（39〜43行）を:

```tsx
      {/* 機械の流体行は従来どおり矢印なし（加工進捗は入出力グリッド間の矢印が担う） */}
      {/* The machine fluid row keeps no arrow; processing progress lives between the in/out grids */}
      <FluidSlotRow fluids={data.fluidSlots} testId="machine-fluid-slots" />
```

に置換（`FluidSlot` の import を `FluidSlotRow` に差し替え）。

- [ ] **Step 3: PowerRateText を作成し2箇所を置換**

`src/features/blockInventory/details/PowerRateText.tsx`:

```tsx
import { Text } from "@mantine/core";
import { computePowerRate } from "./detailLogic";

// 電力率テキスト。不足時は赤表示（uGUI CommonMachineBlockStateDetail 準拠）
// Power-rate text, red when lacking (mirrors uGUI CommonMachineBlockStateDetail)
export default function PowerRateText({
  currentPower,
  requestPower,
  testId,
}: {
  currentPower: number;
  requestPower: number;
  testId: string;
}) {
  const rate = computePowerRate(currentPower, requestPower);
  return (
    <Text size="sm" c={rate < 1 ? "red.5" : "dark.1"} data-testid={testId}>
      電力 {Math.round(rate * 100)}% ({currentPower}/{requestPower})
    </Text>
  );
}
```

`MachineSection.tsx` の `<Text ...>電力 ...</Text>`（44〜46行）を `<PowerRateText currentPower={data.machine.currentPower} requestPower={data.machine.requestPower} testId="machine-power-rate" />` に、`MinerSection.tsx` の同型ブロック（17〜19行）を `<PowerRateText currentPower={data.miner.currentPower} requestPower={data.miner.requestPower} testId="miner-power-rate" />` に置換。両ファイルで不要になった `computePowerRate` 呼び出し・`powerRate`/`lacking` 変数・`Text` import を整理する（MachineSection は他で `Text` 未使用になるため import から外す）。

- [ ] **Step 4: 小修正3件**

`src/features/blockInventory/details/detailLogic.ts` の `fuelRatio`（24〜27行）を共有 clamp01 使用に:

```ts
import { clamp01 } from "@/shared/clamp01";

// 残燃料/満燃料の比を 0..1 にクランプ（分母0は0扱い）。uGUI Generatorの燃料バー相当
// Clamp remaining/full fuel ratio to 0..1 (zero denominator → 0); mirrors the uGUI generator fuel bar
export function fuelRatio(remainingFuelTime: number, currentFuelTime: number): number {
  if (currentFuelTime <= 0) return 0;
  return clamp01(remainingFuelTime / currentFuelTime);
}
```

`src/features/recipe/views/RecipeContent.tsx`: `useState` 群の直後に追加（import に `useEffect` を追加）:

```tsx
  // topic 更新でタブ構成が変わって先頭タブへフォールバックする際、ページ位置の持ち越しを防ぐ
  // When a topic update drops the active tab and we fall back to the first one, reset the page index too
  useEffect(() => {
    if (tabs.length === 0) return;
    if (!tabs.some((t) => t.key === tabKey)) {
      setTabKey(tabs[0].key);
      setRecipeIndex(0);
    }
  }, [tabs, tabKey]);
```

`src/features/blockInventory/details/MinerSection.tsx` の miningItems map を key 衝突しない形に:

```tsx
        {data.miner.miningItems.map((m, i) => (
          <Group key={`${m.itemId}-${i}`} gap={4}>
```

- [ ] **Step 5: 回帰**

Run: `pnpm build && pnpm test` → PASS
Run: `pnpm exec playwright test --config e2e/playwright.config.ts fluidSlot blockDetails recipe` → PASS（tank-body / generic-block-fluids / machine-fluid-slots / *-power-rate の testid は不変なので既存 e2e がそのまま通る）

- [ ] **Step 6: Commit**

```bash
git add src/shared/ui/ src/features/
git commit -m "refactor(webui): FluidSlotRow/PowerRateTextを抽出しfuelRatio・タブindex持ち越し・key衝突を修正"
```

---

### Task 12: 最終検証

- [ ] **Step 1: 依存方向・重複排除の最終グレップ**

すべて0件であること:

```bash
grep -rn "@/app/" src/features/ src/shared/ src/bridge/
grep -rn "@/features/inventory" src/features/blockInventory/
grep -rn "@/features/blockInventory" src/features/inventory/
grep -rn "type ActionRecord" e2e/tests/
grep -rn "blockLogic\|inventoryLogic\|app/uiStore" src/ e2e/
```

- [ ] **Step 2: 全テスト**

Run: `pnpm build` → PASS（型）
Run: `pnpm test` → PASS（unit 全件）
Run: `pnpm test:e2e` → PASS（e2e 全件。grabOverlay / machineGestures の新規2specを含む）

- [ ] **Step 3: 未コミット差分が無いことを確認してまとめ**

```bash
git status --short   # 空であること
git log --oneline -12
```

12コミット（Task 1〜11 + 必要なら修正コミット）が積まれていることを確認する。

---

## Self-Review 結果（作成時に実施済み）

- **指摘カバレッジ**: レポート1の#1〜#8 → Task 5/6/7（#1,#3）・Task 1（#2）・Task 10（#4）・Task 8（#5）・Task 11（#6,#7）・Task 4（#8）。レポート2の#1〜#6 → Task 7（#1,#4）・Task 8/9（#2）・Task 5（#3）・Task 10（#5）・Task 9（#6の一部）。レポート3 → Task 2（useItemMaster）・Task 3（GrabOverlay）・Task 11（RecipeContent/MinerSection）・ModalHost は確認済み対応不要。低優先の未対応項目は冒頭「対応しないと決めた項目」に明記
- **型整合**: `PlannedAction`/`PlayerSlotContext`/`BlockSlotContext`/`useBlockSlotGestures` の名前・シグネチャは Task 5→6→7 で一貫。`UiStateNames`/`ACTION_TYPES` は protocol.ts 起点で全taskが同名参照
- **順序依存**: Task 4（e2e/support）は Task 7 の machineGestures.spec が `payloadsOf`/`setBlock` を使うため先行必須。Task 5 は Task 6/7 の前提。Task 8 と 9 は App.tsx を続けて触るため隣接順で実施
