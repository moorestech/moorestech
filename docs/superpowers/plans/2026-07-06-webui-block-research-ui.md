# WebUI ブロック詳細UI×5 + 研究ツリー Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** React WebUI に発電機/機械/採掘機/ギア機械/フィルタ分岐器のブロック詳細UIと研究ツリーを実装する（spec: `docs/superpowers/specs/2026-07-06-webui-block-research-ui-design.md`）

**Architecture:** ブロック詳細は既存 `block_inventory.current` topic に capability 合成（`machine?`/`gear?` 等の optional）で同梱（D1）。研究は新topic `research.tree`（nodes のみ。**表示可否は既存 `ui_state.current` から導出** — specの open union は実装時に不要と判明、SSOT改善）。C#は「WebUiHost Topic/Action → WireFixtures → TS手書き型」の既存型を踏襲し、**Client.Game（uGUI側）の編集はゼロ**（並行UIStateセッションとの衝突回避。状態検知はサーバーイベント直接購読、ブロック解決は既存公開の datastore/getter のみ使用）。

**並行セッション注意:** 別セッションが uGUI UIState ↔ Web 同期を変更中。本計画で `uiScreenRouting.ts` / `App.tsx` / `activeLayer.ts` / mock-host の uiState 系（Task 13/9）を触る際は追加を最小行に留め、Task 14 でマージ前 rebase 確認を行う。

**Tech Stack:** C# (Unity, UniTask, Newtonsoft JSON camelCase+NullIgnore) / TypeScript (React 18, Mantine v8, zustand, vitest, Playwright)

## Global Constraints

- 1ファイル200行以下。partial 絶対禁止。1ディレクトリ10コードファイルまで
- try-catch 原則禁止（既存の境界例外処理は除く）。デフォルト引数禁止
- コメントは「// 日本語 → // English」2行セット（各1行厳守）、3〜10行ごと。自明コメント禁止
- .meta ファイル手動作成禁止（Unity起動で生成されたものはコミット可）
- C# 変更後は必ず `uloop compile --project-path ./moorestech_client`（ErrorCount 0 必須）
- uGUI凍結: uGUI/ドメイン側への変更は読み取り用 getter/event の additive 追加のみ
- ワイヤ規約: C# `WebUiJson`（camelCase / NullValueHandling.Ignore = nullキー省略）。enum は camelCase 文字列
- 作業ディレクトリ: `/Users/katsumi/moorestech-worktrees/tree2`（最初に `pwd` 確認）。web は `moorestech_web/webui`（pnpm）
- テストコマンド: `pnpm vitest run`（webui内）/ `pnpm exec playwright test`（webui内）/ `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "WireContract"`

## 配置と前例（spec-architecture-review 済み）

| 配置決定 | 前例 |
|---|---|
| ブロック状態変更の検知は WebUiHost が**サーバーイベントを直接購読**（uGUI編集ゼロ） | `CraftingRecipesTopic.cs:32` が `UnlockedEventPacket.EventTag` を直接購読する前例。データは既存公開 `BlockGameObject.GetStateDetail<T>()`（`BlockGameObject.cs:177`）で読む |
| BlockGameObject の解決は `ClientDIContext.BlockGameObjectDataStore.TryGetBlockGameObject(pos)` | `ClientDIContext.cs:9` の既存公開static。`BlockSubInventorySource.BlockPosition`（既存公開）から引く |
| DTO/ビルダーは `Client.WebUiHost/Game/Topics/BlockDetail/` 新設 | `Game/Topics/*.cs` の既存Topic群。200行制約のためサブディレクトリ分割 |
| StateDetail の解釈（DTO化）は WebUiHost 層 | `InventoryTopic` が uGUI公開getterをDTO化する前例と同じ。uGUI/ドメインには置かない |
| マスタ値（slotLayout/baseRpm等）は `MasterHolder`/`BlockMasterElement` を読むだけ | `BlockSubInventorySource.cs:16-22`（マスタ読み取り公開口） |
| 研究の表示可否は `ui_state.current` から導出（research.tree に open を持たせない） | `App.tsx:28` の `screenForUiState` ルーティング。状態の二重配信を避ける（D2/SSOT） |

---

### Task 1: TS ワイヤ契約 — ブロック詳細 capability 型 + バリデータ

**Files:**
- Modify: `moorestech_web/webui/src/bridge/payloadTypes.ts`
- Modify: `moorestech_web/webui/src/bridge/validators.ts`
- Test: `moorestech_web/webui/src/bridge/validators.test.ts`（新規）

**Interfaces:**
- Produces: `MachineDetailData`/`GeneratorDetailData`/`MinerDetailData`/`GearDetailData`/`ElectricNetworkData`/`GearNetworkData`/`FilterSplitterData` 型と、`BlockInventoryOpen` の optional フィールド `machine?/generator?/miner?/gear?/electricNetwork?/gearNetwork?/filterSplitter?`。後続タスクはこの型名を厳守

- [ ] **Step 1: 失敗するバリデータテストを書く**

`src/bridge/validators.test.ts` を新規作成:

```ts
import { describe, expect, it } from "vitest";
import { validateTopicPayload } from "./validators";
import { Topics } from "./protocol";

const openBase = {
  open: true, blockType: "ElectricMachine", identifier: "(0, 0, 0)", blockName: "電気機械",
  itemSlots: [{ itemId: 1, count: 2 }], fluidSlots: [],
};

describe("validBlockInventory capability details", () => {
  it("accepts machine + electricNetwork details", () => {
    const d = {
      ...openBase,
      progress: 0.5,
      machine: { recipeGuid: "g", currentState: "processing", currentPower: 10, requestPower: 20, slotLayout: { input: 2, output: 1, module: 1 } },
      electricNetwork: { totalGeneratePower: 100, totalRequiredPower: 50, consumerCount: 3, powerRate: 1 },
    };
    expect(validateTopicPayload(Topics.blockInventory, d)).toBe(true);
  });
  it("accepts gear + gearNetwork + generator + miner + filterSplitter details", () => {
    const d = {
      ...openBase,
      generator: { remainingFuelTime: 3, currentFuelTime: 10, operatingRate: 0.5 },
      miner: { currentPower: 1, requestPower: 2, miningItems: [{ itemId: 5, itemsPerMinute: 12 }] },
      gear: { isClockwise: true, currentRpm: 10, currentTorque: 3, baseRpm: 20, baseTorque: 5 },
      gearNetwork: { totalRequiredGearPower: 5, totalGenerateGearPower: 10, stopReason: "none" },
      filterSplitter: { directionCount: 2, filterSlotCountPerDirection: 3, directions: [{ mode: "whitelist", filterItemIds: [1, 0, 0] }, { mode: "default", filterItemIds: [0, 0, 0] }] },
    };
    expect(validateTopicPayload(Topics.blockInventory, d)).toBe(true);
  });
  it("rejects malformed details", () => {
    expect(validateTopicPayload(Topics.blockInventory, { ...openBase, machine: { recipeGuid: 1 } })).toBe(false);
    expect(validateTopicPayload(Topics.blockInventory, { ...openBase, gearNetwork: { totalRequiredGearPower: 1, totalGenerateGearPower: 2, stopReason: 3 } })).toBe(false);
    expect(validateTopicPayload(Topics.blockInventory, { ...openBase, filterSplitter: { directionCount: 1, filterSlotCountPerDirection: 1, directions: [{ mode: "whitelist" }] } })).toBe(false);
  });
  it("still accepts details-less open and closed payloads", () => {
    expect(validateTopicPayload(Topics.blockInventory, openBase)).toBe(true);
    expect(validateTopicPayload(Topics.blockInventory, { open: false })).toBe(true);
  });
});
```

- [ ] **Step 2: テストが失敗することを確認**

Run: `cd moorestech_web/webui && pnpm vitest run src/bridge/validators.test.ts`
Expected: FAIL（machine 等の詳細つき payload が現行 `validBlockInventory` で通っても、malformed 拒否テストが FAIL する — 現行は未知キーを検査しないため `machine: {recipeGuid: 1}` が通ってしまう）

- [ ] **Step 3: payloadTypes.ts に capability 型を追加**

`payloadTypes.ts` の `BlockInventoryOpen` 定義（55-65行）を以下に置換し、直前に詳細型を追加:

```ts
// BLK-2〜5/8 ブロック詳細。capability合成（機能単位optional）でブロック種別unionにしない(spec D1)
// BLK-2..5/8 block details; capability composition (per-feature optionals), never a per-blockType union (spec D1)
export type MachineDetailData = {
  recipeGuid: string;
  currentState: string;
  currentPower: number;
  requestPower: number;
  // itemSlots を 入力→出力→モジュール に分割する位置（uGUIのスロット構成順）
  // Split positions of itemSlots into input→output→module (uGUI slot ordering)
  slotLayout: { input: number; output: number; module: number };
};
export type GeneratorDetailData = { remainingFuelTime: number; currentFuelTime: number; operatingRate: number };
export type MinerDetailData = {
  currentPower: number;
  requestPower: number;
  miningItems: { itemId: number; itemsPerMinute: number }[];
};
export type GearDetailData = { isClockwise: boolean; currentRpm: number; currentTorque: number; baseRpm: number; baseTorque: number };
export type ElectricNetworkData = { totalGeneratePower: number; totalRequiredPower: number; consumerCount: number; powerRate: number };
export type GearNetworkStopReason = "none" | "rocked" | "overRequirePower";
export type GearNetworkData = { totalRequiredGearPower: number; totalGenerateGearPower: number; stopReason: GearNetworkStopReason };
export type FilterSplitterMode = "default" | "whitelist" | "blacklist";
export type FilterSplitterDirectionData = { mode: FilterSplitterMode; filterItemIds: number[] };
export type FilterSplitterData = { directionCount: number; filterSlotCountPerDirection: number; directions: FilterSplitterDirectionData[] };

export type BlockInventoryOpen = {
  open: true;
  blockType: string;
  identifier: string;
  blockName: string;
  itemSlots: SlotData[];
  fluidSlots: FluidSlotData[];
  // progress は null 時にキー省略されるため optional
  // progress is key-omitted when null, so it is optional
  progress?: number;
  // 詳細は該当ブロックのみ付与（C# NullValueHandling.Ignore で非該当キーは省略）
  // Details are attached only for applicable blocks (absent keys omitted via C# NullValueHandling.Ignore)
  machine?: MachineDetailData;
  generator?: GeneratorDetailData;
  miner?: MinerDetailData;
  gear?: GearDetailData;
  electricNetwork?: ElectricNetworkData;
  gearNetwork?: GearNetworkData;
  filterSplitter?: FilterSplitterData;
};
```

- [ ] **Step 4: validators.ts に capability バリデータを追加**

`validators.ts` の `validBlockInventory`（33-44行）を以下に置換（ヘルパは既存を利用）:

```ts
// 各capability詳細のshapeガード。undefined(キー省略)は常に許容する
// Shape guards per capability detail; undefined (omitted key) is always accepted
function validMachineDetail(v: unknown): boolean {
  if (v === undefined) return true;
  if (!isObject(v)) return false;
  const layout = v.slotLayout;
  return (
    isString(v.recipeGuid) && isString(v.currentState) && isNumber(v.currentPower) && isNumber(v.requestPower) &&
    isObject(layout) && isNumber(layout.input) && isNumber(layout.output) && isNumber(layout.module)
  );
}
function validGeneratorDetail(v: unknown): boolean {
  if (v === undefined) return true;
  return isObject(v) && isNumber(v.remainingFuelTime) && isNumber(v.currentFuelTime) && isNumber(v.operatingRate);
}
function validMinerDetail(v: unknown): boolean {
  if (v === undefined) return true;
  return isObject(v) && isNumber(v.currentPower) && isNumber(v.requestPower) &&
    isArrayOf(v.miningItems, (m) => isObject(m) && isNumber(m.itemId) && isNumber(m.itemsPerMinute));
}
function validGearDetail(v: unknown): boolean {
  if (v === undefined) return true;
  return isObject(v) && isBool(v.isClockwise) && isNumber(v.currentRpm) && isNumber(v.currentTorque) && isNumber(v.baseRpm) && isNumber(v.baseTorque);
}
function validElectricNetwork(v: unknown): boolean {
  if (v === undefined) return true;
  return isObject(v) && isNumber(v.totalGeneratePower) && isNumber(v.totalRequiredPower) && isNumber(v.consumerCount) && isNumber(v.powerRate);
}
function validGearNetwork(v: unknown): boolean {
  if (v === undefined) return true;
  return isObject(v) && isNumber(v.totalRequiredGearPower) && isNumber(v.totalGenerateGearPower) && isString(v.stopReason);
}
function validFilterSplitter(v: unknown): boolean {
  if (v === undefined) return true;
  return isObject(v) && isNumber(v.directionCount) && isNumber(v.filterSlotCountPerDirection) &&
    isArrayOf(v.directions, (d) => isObject(d) && isString(d.mode) && isArrayOf(d.filterItemIds, isNumber));
}

// 閉状態は open:false のみ。開状態は基本フィールド必須 + capability詳細は省略可
// Closed is only open:false; open requires base fields, capability details are optional
function validBlockInventory(d: unknown): boolean {
  if (!isObject(d) || !isBool(d.open)) return false;
  if (!d.open) return true;
  return (
    isString(d.blockType) &&
    isString(d.identifier) &&
    isString(d.blockName) &&
    isArrayOf(d.itemSlots, isSlot) &&
    isArrayOf(d.fluidSlots, isFluidSlot) &&
    (d.progress === undefined || isNumber(d.progress)) &&
    validMachineDetail(d.machine) && validGeneratorDetail(d.generator) && validMinerDetail(d.miner) &&
    validGearDetail(d.gear) && validElectricNetwork(d.electricNetwork) && validGearNetwork(d.gearNetwork) &&
    validFilterSplitter(d.filterSplitter)
  );
}
```

注意: validators.ts が200行を超える場合は capability バリデータ群を `src/bridge/blockDetailValidators.ts` に切り出して import する（export は `validMachineDetail` 等の関数群と、まとめ検証の `validBlockDetails(d)`）。

- [ ] **Step 5: テスト成功を確認**

Run: `cd moorestech_web/webui && pnpm vitest run src/bridge/validators.test.ts`
Expected: PASS（4 tests）。既存全テストも: `pnpm vitest run` → 全green

- [ ] **Step 6: Commit**

```bash
git add moorestech_web/webui/src/bridge
git commit -m "feat(webui): block_inventory に capability詳細型とバリデータを追加"
```

---

### Task 2: TS ワイヤ契約 — research.tree topic + 新Action 3種の登録

**Files:**
- Modify: `moorestech_web/webui/src/bridge/payloadTypes.ts`
- Modify: `moorestech_web/webui/src/bridge/protocol.ts`
- Modify: `moorestech_web/webui/src/bridge/validators.ts`
- Test: `moorestech_web/webui/src/bridge/validators.test.ts`

**Interfaces:**
- Produces: `Topics.researchTree = "research.tree"`、型 `ResearchTreeData = { nodes: ResearchNodeData[] }`、`ResearchNodeData`、Action `"research.complete"` / `"filter_splitter.set_mode"` / `"filter_splitter.set_filter_item"`（payload 形状は下記コード）

- [ ] **Step 1: 失敗するテストを追加**

`validators.test.ts` に追記:

```ts
describe("validResearchTree", () => {
  const node = {
    guid: "abc", name: "研究1", description: "説明",
    state: "researchable", position: { x: 100, y: -50 },
    prevGuids: [], consumeItems: [{ itemId: 1, count: 3 }],
    rewardItemIds: [2], unlockItemIds: [],
  };
  it("accepts nodes payload", () => {
    expect(validateTopicPayload(Topics.researchTree, { nodes: [node] })).toBe(true);
    expect(validateTopicPayload(Topics.researchTree, { nodes: [] })).toBe(true);
  });
  it("rejects malformed node", () => {
    expect(validateTopicPayload(Topics.researchTree, { nodes: [{ ...node, position: { x: "0", y: 0 } }] })).toBe(false);
    expect(validateTopicPayload(Topics.researchTree, {})).toBe(false);
  });
});
```

- [ ] **Step 2: 失敗確認**

Run: `pnpm vitest run src/bridge/validators.test.ts`
Expected: FAIL with `Topics.researchTree` undefined（コンパイルエラー）

- [ ] **Step 3: 型・topic・action を登録**

`payloadTypes.ts` 末尾に追加:

```ts
// FEAT-RES-1 研究ツリー。表示可否は ui_state.current(ResearchTree) から導出し、本topicはノードデータのみ運ぶ
// FEAT-RES-1 research tree; visibility derives from ui_state.current (ResearchTree), this topic carries node data only
export type ResearchNodeState =
  | "completed" | "researchable"
  | "unresearchableNotEnoughItem" | "unresearchableNotEnoughPreNode" | "unresearchableAllReasons";
export type ResearchNodeData = {
  guid: string;
  name: string;
  description: string;
  state: ResearchNodeState;
  // マスタ GraphViewSettings.UIPosition。uGUI anchoredPosition と同値
  // Master GraphViewSettings.UIPosition; same value as the uGUI anchoredPosition
  position: { x: number; y: number };
  prevGuids: string[];
  consumeItems: { itemId: number; count: number }[];
  rewardItemIds: number[];
  unlockItemIds: number[];
};
export type ResearchTreeData = { nodes: ResearchNodeData[] };
```

`protocol.ts`: import に `ResearchTreeData` を追加し、`Topics` に `researchTree: "research.tree",`、`TopicPayloads` に `[Topics.researchTree]: ResearchTreeData;` を追加。`ActionPayloads` に追加:

```ts
  "research.complete": { researchGuid: string };
  "filter_splitter.set_mode": { directionIndex: number; mode: "default" | "whitelist" | "blacklist" };
  // clear:true は右クリック相当のフィルタ解除。clear:false は C# 側が Grab の持ち手アイテムを設定する
  // clear:true clears the filter (right-click); with clear:false the C# side assigns the currently grabbed item
  "filter_splitter.set_filter_item": { directionIndex: number; slotIndex: number; clear: boolean };
```

`validators.ts` に追加し `validators` レコードへ `[Topics.researchTree]: validResearchTree,` を登録:

```ts
// 研究ノードは表示に使う全フィールドを検査する（不正ノード1件で全体破棄）
// Validate every displayed field of research nodes (one bad node drops the whole payload)
function validResearchNode(v: unknown): boolean {
  return isObject(v) && isString(v.guid) && isString(v.name) && isString(v.description) && isString(v.state) &&
    isObject(v.position) && isNumber(v.position.x) && isNumber(v.position.y) &&
    isArrayOf(v.prevGuids, isString) &&
    isArrayOf(v.consumeItems, (c) => isObject(c) && isNumber(c.itemId) && isNumber(c.count)) &&
    isArrayOf(v.rewardItemIds, isNumber) && isArrayOf(v.unlockItemIds, isNumber);
}
function validResearchTree(d: unknown): boolean {
  return isObject(d) && isArrayOf(d.nodes, validResearchNode);
}
```

- [ ] **Step 4: テスト成功確認**

Run: `pnpm vitest run` → 全green

- [ ] **Step 5: Commit**

```bash
git add moorestech_web/webui/src/bridge
git commit -m "feat(webui): research.tree topic とフィルタ分岐器/研究のAction契約を追加"
```

---

### Task 3: WireFixtures（C#⇔TS共有契約フィクスチャ）+ TS 側 wireContract テスト

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Tests/WebUi/WireFixtures/block_inventory_machine.json`
- Create: `moorestech_client/Assets/Scripts/Client.Tests/WebUi/WireFixtures/block_inventory_gear_machine.json`
- Create: `moorestech_client/Assets/Scripts/Client.Tests/WebUi/WireFixtures/block_inventory_generator.json`
- Create: `moorestech_client/Assets/Scripts/Client.Tests/WebUi/WireFixtures/block_inventory_miner.json`
- Create: `moorestech_client/Assets/Scripts/Client.Tests/WebUi/WireFixtures/block_inventory_filter_splitter.json`
- Create: `moorestech_client/Assets/Scripts/Client.Tests/WebUi/WireFixtures/research_tree.json`
- Modify: `moorestech_web/webui/src/bridge/wireContract.test.ts`

**Interfaces:**
- Produces: 6つの共有フィクスチャ（C# Task 4 の `AssertMatchesFixture` が同一ファイルを参照する）
- .meta は手動作成しない（Task 4 の uloop compile で Unity が生成したものをコミット）

- [ ] **Step 1: フィクスチャ JSON を作成**

`block_inventory_machine.json`（ElectricMachine の代表形。fluid + progress + machine + electricNetwork）:

```json
{
  "open": true,
  "blockType": "ElectricMachine",
  "identifier": "(1, 0, 2)",
  "blockName": "電気機械",
  "itemSlots": [
    { "itemId": 3, "count": 5 },
    { "itemId": 0, "count": 0 },
    { "itemId": 7, "count": 1 },
    { "itemId": 0, "count": 0 }
  ],
  "fluidSlots": [
    { "fluidId": 1, "amount": 25.5, "capacity": 100.0, "name": "水" }
  ],
  "progress": 0.42,
  "machine": {
    "recipeGuid": "00000000-0000-0000-0000-000000000000",
    "currentState": "processing",
    "currentPower": 80.0,
    "requestPower": 100.0,
    "slotLayout": { "input": 2, "output": 1, "module": 1 }
  },
  "electricNetwork": {
    "totalGeneratePower": 500.0,
    "totalRequiredPower": 300.0,
    "consumerCount": 4,
    "powerRate": 1.0
  }
}
```

`block_inventory_gear_machine.json`（GearMachine。machine + gear + gearNetwork。electricNetwork 無し）:

```json
{
  "open": true,
  "blockType": "GearMachine",
  "identifier": "(0, 0, 0)",
  "blockName": "ギア機械",
  "itemSlots": [ { "itemId": 3, "count": 2 }, { "itemId": 0, "count": 0 } ],
  "fluidSlots": [],
  "progress": 0.1,
  "machine": {
    "recipeGuid": "00000000-0000-0000-0000-000000000000",
    "currentState": "idle",
    "currentPower": 0.0,
    "requestPower": 0.0,
    "slotLayout": { "input": 1, "output": 1, "module": 0 }
  },
  "gear": { "isClockwise": true, "currentRpm": 12.5, "currentTorque": 3.0, "baseRpm": 20.0, "baseTorque": 5.0 },
  "gearNetwork": { "totalRequiredGearPower": 60.0, "totalGenerateGearPower": 100.0, "stopReason": "none" }
}
```

`block_inventory_generator.json`（ElectricGenerator。generator + electricNetwork。progress 無し=キー省略の omission ケース）:

```json
{
  "open": true,
  "blockType": "ElectricGenerator",
  "identifier": "(5, 0, 5)",
  "blockName": "発電機",
  "itemSlots": [ { "itemId": 9, "count": 30 } ],
  "fluidSlots": [],
  "generator": { "remainingFuelTime": 12.5, "currentFuelTime": 30.0, "operatingRate": 0.75 },
  "electricNetwork": { "totalGeneratePower": 200.0, "totalRequiredPower": 150.0, "consumerCount": 2, "powerRate": 1.0 }
}
```

`block_inventory_miner.json`（ElectricMiner。miner + electricNetwork + progress）:

```json
{
  "open": true,
  "blockType": "ElectricMiner",
  "identifier": "(3, 0, 8)",
  "blockName": "電動採掘機",
  "itemSlots": [ { "itemId": 11, "count": 42 }, { "itemId": 0, "count": 0 } ],
  "fluidSlots": [],
  "progress": 0.66,
  "miner": {
    "currentPower": 50.0,
    "requestPower": 100.0,
    "miningItems": [ { "itemId": 11, "itemsPerMinute": 12.0 } ]
  },
  "electricNetwork": { "totalGeneratePower": 100.0, "totalRequiredPower": 100.0, "consumerCount": 1, "powerRate": 1.0 }
}
```

`block_inventory_filter_splitter.json`（FilterSplitter。itemSlots 空・filterSplitter のみ）:

```json
{
  "open": true,
  "blockType": "FilterSplitter",
  "identifier": "(2, 0, 2)",
  "blockName": "フィルタ分岐器",
  "itemSlots": [],
  "fluidSlots": [],
  "filterSplitter": {
    "directionCount": 3,
    "filterSlotCountPerDirection": 2,
    "directions": [
      { "mode": "whitelist", "filterItemIds": [4, 0] },
      { "mode": "default", "filterItemIds": [0, 0] },
      { "mode": "blacklist", "filterItemIds": [7, 8] }
    ]
  }
}
```

`research_tree.json`:

```json
{
  "nodes": [
    {
      "guid": "11111111-1111-1111-1111-111111111111",
      "name": "最初の研究",
      "description": "説明テキスト",
      "state": "completed",
      "position": { "x": 0.0, "y": 0.0 },
      "prevGuids": [],
      "consumeItems": [ { "itemId": 1, "count": 5 } ],
      "rewardItemIds": [ 2 ],
      "unlockItemIds": []
    },
    {
      "guid": "22222222-2222-2222-2222-222222222222",
      "name": "次の研究",
      "description": "前提つき",
      "state": "unresearchableNotEnoughPreNode",
      "position": { "x": 300.0, "y": -120.0 },
      "prevGuids": [ "11111111-1111-1111-1111-111111111111" ],
      "consumeItems": [],
      "rewardItemIds": [],
      "unlockItemIds": [ 3 ]
    }
  ]
}
```

- [ ] **Step 2: TS wireContract テストを追加**

`wireContract.test.ts` の既存 `readFixture` ヘルパを使い、既存テスト群の下に追記:

```ts
describe("block detail fixtures", () => {
  const cases = [
    "block_inventory_machine.json",
    "block_inventory_gear_machine.json",
    "block_inventory_generator.json",
    "block_inventory_miner.json",
    "block_inventory_filter_splitter.json",
  ];
  for (const file of cases) {
    it(`accepts ${file} and types it as open`, () => {
      const data = readFixture(file);
      expect(validateTopicPayload(Topics.blockInventory, data)).toBe(true);
      const payload = data as BlockInventoryData;
      if (!payload.open) throw new Error("fixture must be open");
      expect(payload.blockType.length).toBeGreaterThan(0);
    });
  }
  it("consumes capability fields with the declared types", () => {
    const machine = readFixture("block_inventory_machine.json") as BlockInventoryData;
    if (!machine.open || !machine.machine) throw new Error("machine fixture shape");
    expect(machine.machine.slotLayout.input + machine.machine.slotLayout.output + machine.machine.slotLayout.module).toBe(machine.itemSlots.length);
    const gear = readFixture("block_inventory_gear_machine.json") as BlockInventoryData;
    if (!gear.open || !gear.gearNetwork) throw new Error("gear fixture shape");
    expect(["none", "rocked", "overRequirePower"]).toContain(gear.gearNetwork.stopReason);
  });
});

describe("research_tree fixture", () => {
  it("accepts and types research payload", () => {
    const data = readFixture("research_tree.json");
    expect(validateTopicPayload(Topics.researchTree, data)).toBe(true);
    const tree = data as ResearchTreeData;
    expect(tree.nodes.length).toBe(2);
    expect(tree.nodes[1].prevGuids).toContain(tree.nodes[0].guid);
  });
});
```

（import に `ResearchTreeData` を追加。`readFixture` が単一ファイル名引数でない場合は既存の読み方に合わせる）

- [ ] **Step 3: テスト実行**

Run: `pnpm vitest run src/bridge/wireContract.test.ts`
Expected: PASS

- [ ] **Step 4: Commit**

```bash
git add moorestech_client/Assets/Scripts/Client.Tests/WebUi/WireFixtures moorestech_web/webui/src/bridge/wireContract.test.ts
git commit -m "feat(webui): ブロック詳細と研究ツリーのWireFixturesを追加しTS側契約テストを整備"
```

---

### Task 4: C# DTO 群 + C# WireContractTest

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Topics/BlockDetail/BlockDetailDtos.cs`
- Create: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Topics/ResearchTopicDtos.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Topics/BlockInventoryTopic.cs`（DTOにフィールド追加）
- Create: `moorestech_client/Assets/Scripts/Client.Tests/WebUi/WireContractBlockDetailTest.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.WebUiHost/Client.WebUiHost.asmdef`

**Interfaces:**
- Consumes: Task 3 のフィクスチャ
- Produces: `MachineDetailDto`/`GeneratorDetailDto`/`MinerDetailDto`/`GearDetailDto`/`ElectricNetworkDto`/`GearNetworkDto`/`FilterSplitterDto`/`FilterSplitterDirectionDto`/`SlotLayoutDto`/`MiningItemDto`（namespace `Client.WebUiHost.Game.Topics.BlockDetail`）、`ResearchTreeDto`/`ResearchNodeDto`（namespace `Client.WebUiHost.Game.Topics`）。`BlockInventoryDto` に7つの詳細フィールド追加

- [ ] **Step 1: asmdef に参照を追加**

`Client.WebUiHost.asmdef` の references に追加: `"Game.Gear"`, `"Server.Protocol"`, `"Server.Util"`, `"Game.Research"`
（`Game.Block.Interface` の StateDetail 型が解決できない場合は `"Game.Block.Interface"` と `"Game.Block"` も追加。コンパイルで確認）

- [ ] **Step 2: BlockDetailDtos.cs を作成**

```csharp
using System.Collections.Generic;

namespace Client.WebUiHost.Game.Topics.BlockDetail
{
    /// <summary>
    /// block_inventory.current の capability 詳細 DTO（spec D1: 機能単位合成）
    /// Capability detail DTOs for block_inventory.current (spec D1: per-feature composition)
    /// </summary>
    public class MachineDetailDto
    {
        public string RecipeGuid;
        public string CurrentState;
        public float CurrentPower;
        public float RequestPower;
        public SlotLayoutDto SlotLayout;
    }

    public class SlotLayoutDto
    {
        public int Input;
        public int Output;
        public int Module;
    }

    public class GeneratorDetailDto
    {
        public double RemainingFuelTime;
        public double CurrentFuelTime;
        public float OperatingRate;
    }

    public class MinerDetailDto
    {
        public float CurrentPower;
        public float RequestPower;
        public List<MiningItemDto> MiningItems;
    }

    public class MiningItemDto
    {
        public int ItemId;
        public float ItemsPerMinute;
    }

    public class GearDetailDto
    {
        public bool IsClockwise;
        public float CurrentRpm;
        public float CurrentTorque;
        public float BaseRpm;
        public float BaseTorque;
    }

    public class ElectricNetworkDto
    {
        public float TotalGeneratePower;
        public float TotalRequiredPower;
        public int ConsumerCount;
        public float PowerRate;
    }

    public class GearNetworkDto
    {
        public float TotalRequiredGearPower;
        public float TotalGenerateGearPower;
        public string StopReason;
    }

    public class FilterSplitterDto
    {
        public int DirectionCount;
        public int FilterSlotCountPerDirection;
        public List<FilterSplitterDirectionDto> Directions;
    }

    public class FilterSplitterDirectionDto
    {
        public string Mode;
        public List<int> FilterItemIds;
    }
}
```

- [ ] **Step 3: BlockInventoryDto に詳細フィールドを追加**

`BlockInventoryTopic.cs` の `BlockInventoryDto`（112-121行）へ追加（using に `Client.WebUiHost.Game.Topics.BlockDetail` を追加）:

```csharp
    public class BlockInventoryDto
    {
        public bool Open;
        public string BlockType;
        public string Identifier;
        public string BlockName;
        public List<BlockItemSlotDto> ItemSlots;
        public List<BlockFluidSlotDto> FluidSlots;
        public double? Progress;
        // capability 詳細（該当ブロックのみ。null はキー省略される）
        // Capability details (only for applicable blocks; null keys are omitted)
        public MachineDetailDto Machine;
        public GeneratorDetailDto Generator;
        public MinerDetailDto Miner;
        public GearDetailDto Gear;
        public ElectricNetworkDto ElectricNetwork;
        public GearNetworkDto GearNetwork;
        public FilterSplitterDto FilterSplitter;
    }
```

- [ ] **Step 4: ResearchTopicDtos.cs を作成**

```csharp
using System.Collections.Generic;

namespace Client.WebUiHost.Game.Topics
{
    /// <summary>
    /// research.tree の配信 DTO。表示可否は ui_state.current 側で判定するため open を持たない
    /// Payload DTO for research.tree; no open flag because visibility derives from ui_state.current
    /// </summary>
    public class ResearchTreeDto
    {
        public List<ResearchNodeDto> Nodes;
    }

    public class ResearchNodeDto
    {
        public string Guid;
        public string Name;
        public string Description;
        public string State;
        public ResearchPositionDto Position;
        public List<string> PrevGuids;
        public List<ResearchConsumeItemDto> ConsumeItems;
        public List<int> RewardItemIds;
        public List<int> UnlockItemIds;
    }

    public class ResearchPositionDto
    {
        public double X;
        public double Y;
    }

    public class ResearchConsumeItemDto
    {
        public int ItemId;
        public int Count;
    }
}
```

- [ ] **Step 5: WireContractBlockDetailTest.cs を作成**

既存 `WireContractTest.cs` の `LoadFixture`/`AssertMatchesFixture` パターンを同ファイル内に再掲して独立させる（Client.Tests の既存ヘルパが private のため）。フィクスチャと同値の DTO を C# で組み立てて照合:

```csharp
using System.Collections.Generic;
using System.IO;
using Client.WebUiHost.Common;
using Client.WebUiHost.Game.Topics;
using Client.WebUiHost.Game.Topics.BlockDetail;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.WebUi
{
    /// <summary>
    /// ブロック詳細/研究の DTO ⇔ WireFixtures の一致を C# 側から強制する
    /// Enforce DTO ⇔ WireFixtures equality for block details and research from the C# side
    /// </summary>
    public class WireContractBlockDetailTest
    {
        [Test]
        public void GearMachineFixtureMatchesDto()
        {
            var dto = new BlockInventoryDto
            {
                Open = true,
                BlockType = "GearMachine",
                Identifier = "(0, 0, 0)",
                BlockName = "ギア機械",
                ItemSlots = new List<BlockItemSlotDto>
                {
                    new() { ItemId = 3, Count = 2 },
                    new() { ItemId = 0, Count = 0 },
                },
                FluidSlots = new List<BlockFluidSlotDto>(),
                Progress = 0.1,
                Machine = new MachineDetailDto
                {
                    RecipeGuid = "00000000-0000-0000-0000-000000000000",
                    CurrentState = "idle",
                    CurrentPower = 0f,
                    RequestPower = 0f,
                    SlotLayout = new SlotLayoutDto { Input = 1, Output = 1, Module = 0 },
                },
                Gear = new GearDetailDto { IsClockwise = true, CurrentRpm = 12.5f, CurrentTorque = 3f, BaseRpm = 20f, BaseTorque = 5f },
                GearNetwork = new GearNetworkDto { TotalRequiredGearPower = 60f, TotalGenerateGearPower = 100f, StopReason = "none" },
            };
            AssertMatchesFixture(dto, "block_inventory_gear_machine.json");
        }

        [Test]
        public void ResearchTreeFixtureMatchesDto()
        {
            var dto = new ResearchTreeDto
            {
                Nodes = new List<ResearchNodeDto>
                {
                    new()
                    {
                        Guid = "11111111-1111-1111-1111-111111111111",
                        Name = "最初の研究",
                        Description = "説明テキスト",
                        State = "completed",
                        Position = new ResearchPositionDto { X = 0, Y = 0 },
                        PrevGuids = new List<string>(),
                        ConsumeItems = new List<ResearchConsumeItemDto> { new() { ItemId = 1, Count = 5 } },
                        RewardItemIds = new List<int> { 2 },
                        UnlockItemIds = new List<int>(),
                    },
                    new()
                    {
                        Guid = "22222222-2222-2222-2222-222222222222",
                        Name = "次の研究",
                        Description = "前提つき",
                        State = "unresearchableNotEnoughPreNode",
                        Position = new ResearchPositionDto { X = 300, Y = -120 },
                        PrevGuids = new List<string> { "11111111-1111-1111-1111-111111111111" },
                        ConsumeItems = new List<ResearchConsumeItemDto>(),
                        RewardItemIds = new List<int>(),
                        UnlockItemIds = new List<int> { 3 },
                    },
                },
            };
            AssertMatchesFixture(dto, "research_tree.json");
        }

        // DTO を実運用シリアライザで直列化しフィクスチャと DeepEquals 照合する
        // Serialize the DTO with the production serializer and DeepEquals against the fixture
        private void AssertMatchesFixture(object dto, string fixtureName)
        {
            var actual = JToken.Parse(WebUiJson.Serialize(dto));
            var expected = JToken.Parse(LoadFixture(fixtureName));
            Assert.IsTrue(JToken.DeepEquals(expected, actual), $"fixture mismatch: {fixtureName}\nexpected: {expected}\nactual: {actual}");
        }

        private string LoadFixture(string fixtureName)
        {
            var path = Path.Combine(Application.dataPath, "Scripts/Client.Tests/WebUi/WireFixtures", fixtureName);
            return File.ReadAllText(path);
        }
    }
}
```

（`#region Internal` はクラス直下では規約違反のため使わない。既存 `WireContractTest.cs` に同名ヘルパがある場合は命名衝突しない別クラスなので問題ない）

同様に `ElectricMachine`（machine+electricNetwork+fluid+progress で `block_inventory_machine.json`）、`ElectricGenerator`（progress 省略ケースの omission 検証込み）、`ElectricMiner`、`FilterSplitter` の各 `[Test]` を、フィクスチャ値と同値で追加する。200行を超える場合はブロック系と研究系でテストクラスを2ファイルに分割する（`WireContractResearchTest.cs`）。

- [ ] **Step 6: コンパイルとテスト**

```bash
uloop compile --project-path ./moorestech_client
uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "WireContract"
```
Expected: ErrorCount 0 / 全テスト green（TS側も `pnpm vitest run src/bridge/wireContract.test.ts` で再確認）

- [ ] **Step 7: Commit（Unity生成の .meta も含める）**

```bash
git add moorestech_client/Assets/Scripts/Client.WebUiHost moorestech_client/Assets/Scripts/Client.Tests/WebUi
git commit -m "feat(webui-host): ブロック詳細/研究DTOとC#側WireContractテストを追加"
```

---

### Task 5: ~~uGUI への additive フック~~（**削除済み — 不要**）

**削除理由（再チェック 2026-07-06）:** 並行セッションが uGUI UIState 同期を変更中のため Client.Game 編集を避ける。かつ両フックとも既存機構で代替可能:
- `OnBlockStateChanged` イベント → WebUiHost が `ClientContext.VanillaApi.Event.SubscribeEventResponse(ChangeBlockStateEventPacket.CreateSpecifiedBlockEventTag(...))` で**サーバーイベントを直接購読**（`CraftingRecipesTopic.cs:32` と同型の前例）。データは既存公開の `BlockGameObject.GetStateDetail<T>()` で読む
- `BlockSubInventorySource.BlockGameObject` getter → `ClientDIContext.BlockGameObjectDataStore.TryGetBlockGameObject(blockSource.BlockPosition, out var block)`（`ClientDIContext.cs:9` の既存公開static + `BlockGameObjectDataStore.cs:42`）

実装は Task 6 に統合済み。**このタスクでは何もしない。**

---

### Task 6: C# capability ビルダー + BlockInventoryTopic 組み込み

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Topics/BlockDetail/BlockDetailDtoBuilder.cs`
- Create: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Topics/BlockDetail/BlockNetworkInfoCache.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Topics/BlockInventoryTopic.cs`

**Interfaces:**
- Consumes: Task 4 の DTO、既存公開の `ClientDIContext.BlockGameObjectDataStore` / `BlockGameObject.GetStateDetail<T>()` / `ChangeBlockStateEventPacket.CreateSpecifiedBlockEventTag`（uGUI編集なし）
- Produces: `BlockDetailDtoBuilder.Apply(BlockInventoryDto dto, BlockGameObject block, BlockNetworkInfoCache cache)`（静的メソッド。dto の詳細フィールドを充填）、`BlockNetworkInfoCache`（electric/gear/filterSplitter の取得結果キャッシュ + `OnUpdated` event Action）。`BlockInventoryTopic.NetworkCache`（Task 8 の action handler がスナップショット反映に使う公開口）

- [ ] **Step 1: BlockNetworkInfoCache.cs を作成**

開いているブロックの「都度取得系」情報を保持し、取得完了で `OnUpdated` を発火する。electric は 1 秒間隔ポーリング、gear と filterSplitter は開時 1 回:

```csharp
using System;
using System.Threading;
using Client.Game.InGame.Block;
using Client.Game.InGame.Context;
using Cysharp.Threading.Tasks;
using Server.Protocol.PacketResponse;
using UnityEngine;

namespace Client.WebUiHost.Game.Topics.BlockDetail
{
    /// <summary>
    /// 開いているブロックのネットワーク集約情報を取得・キャッシュする（electric=1秒ポーリング / gear・filter=開時1回）
    /// Fetches and caches network aggregates for the open block (electric = 1s polling; gear/filter = once on open)
    /// </summary>
    public class BlockNetworkInfoCache
    {
        public GetElectricNetworkInfoProtocol.ElectricNetworkInfoSnapshot Electric { get; private set; }
        public GetGearNetworkInfoProtocol.GearNetworkInfoSnapshot GearNetwork { get; private set; }
        public FilterSplitterStateProtocol.FilterSplitterStateResponse FilterSplitter { get; private set; }
        public event Action OnUpdated;

        private CancellationTokenSource _cts;
        private Vector3Int _currentPos;

        // 開いたブロックに合わせて取得を開始する。ブロックが変わったら前の取得を打ち切る
        // Start fetching for the opened block; cancel previous fetches when the block changes
        public void Track(BlockGameObject block, bool electric, bool gear, bool filterSplitter)
        {
            if (block != null && block.BlockPosInfo.OriginalPos == _currentPos && _cts != null) return;
            Clear();
            if (block == null) return;
            _currentPos = block.BlockPosInfo.OriginalPos;
            _cts = new CancellationTokenSource();
            if (electric) PollElectric(block, _cts.Token).Forget();
            if (gear) FetchGear(block, _cts.Token).Forget();
            if (filterSplitter) FetchFilterSplitter(block.BlockPosInfo.OriginalPos, _cts.Token).Forget();
        }

        public void Clear()
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
            Electric = null;
            GearNetwork = null;
            FilterSplitter = null;
            _currentPos = new Vector3Int(int.MinValue, int.MinValue, int.MinValue);
        }

        // action handler が SetMode/SetFilterItem 応答のスナップショットを反映する書き込み口
        // Write access for action handlers to apply SetMode/SetFilterItem response snapshots
        public void ApplyFilterSplitterSnapshot(FilterSplitterStateProtocol.FilterSplitterStateResponse response)
        {
            FilterSplitter = response;
            OnUpdated?.Invoke();
        }

        private async UniTaskVoid PollElectric(BlockGameObject block, CancellationToken ct)
        {
            // uGUI ElectricNetworkInfoView と同じ1秒間隔ポーリング
            // Same 1-second polling as uGUI ElectricNetworkInfoView
            while (!ct.IsCancellationRequested)
            {
                var response = await ClientContext.VanillaApi.Response.GetElectricNetworkInfo(block.BlockInstanceId, ct);
                if (ct.IsCancellationRequested) return;
                Electric = response?.Snapshot;
                OnUpdated?.Invoke();
                await UniTask.Delay(TimeSpan.FromSeconds(1), cancellationToken: ct);
            }
        }

        private async UniTaskVoid FetchGear(BlockGameObject block, CancellationToken ct)
        {
            var response = await ClientContext.VanillaApi.Response.GetGearNetworkInfo(block.BlockInstanceId, ct);
            if (ct.IsCancellationRequested) return;
            GearNetwork = response?.Info;
            OnUpdated?.Invoke();
        }

        private async UniTaskVoid FetchFilterSplitter(Vector3Int pos, CancellationToken ct)
        {
            var request = FilterSplitterStateProtocol.FilterSplitterStateRequest.CreateGetRequest(pos);
            var response = await ClientContext.VanillaApi.Response.SendFilterSplitterStateRequest(request, ct);
            if (ct.IsCancellationRequested) return;
            ApplyFilterSplitterSnapshot(response);
        }
    }
}
```

実装時の確認点（コンパイルで検出される）: `GetElectricNetworkInfoProtocol` の応答スナップショットのプロパティ名は `Snapshot` でない可能性がある。`moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/GetElectricNetworkInfoProtocol.cs` を読み、`ElectricNetworkInfoSnapshot` を取り出す実プロパティ名（uGUI `ElectricNetworkInfoView.cs:35-52` の使用箇所が正）に合わせる。`ct.IsCancellationRequested` の代わりに `UniTask` のキャンセル例外設計を使う場合は uGUI `FilterSplitterBlockInventoryView.cs:57-75` のキャンセルガードの形に合わせる。

- [ ] **Step 2: BlockDetailDtoBuilder.cs を作成**

StateDetail の**存在**で capability を判定し dto に充填する（D1）:

```csharp
using System.Collections.Generic;
using Client.Game.InGame.Block;
using Core.Master;
using Game.Block.Blocks.PowerGenerator;
using Game.Block.Interface.State;
using Game.Gear.Common;
using Mooresmaster.Model.BlocksModule;

namespace Client.WebUiHost.Game.Topics.BlockDetail
{
    /// <summary>
    /// BlockGameObject の StateDetail 群とマスタ値から capability 詳細を dto へ充填する
    /// Fills capability details into the dto from the BlockGameObject's state details and master values
    /// </summary>
    public static class BlockDetailDtoBuilder
    {
        public static void Apply(BlockInventoryDto dto, BlockGameObject block, BlockNetworkInfoCache cache)
        {
            var param = block.BlockMasterElement.BlockParam;

            // 機械系: CommonMachine + MachineBlock の両 StateDetail が揃うブロックのみ
            // Machines: only blocks carrying both CommonMachine and MachineBlock state details
            var common = block.GetStateDetail<CommonMachineBlockStateDetail>(CommonMachineBlockStateDetail.BlockStateDetailKey);
            var machineState = block.GetStateDetail<MachineBlockStateDetail>(MachineBlockStateDetail.BlockStateDetailKey);
            if (common != null && machineState != null && param is IMachineParam machineParam)
            {
                dto.Progress = machineState.ProcessingRate;
                dto.Machine = new MachineDetailDto
                {
                    RecipeGuid = machineState.MachineRecipeGuid,
                    CurrentState = ToCamelCase(common.CurrentStateType),
                    CurrentPower = common.CurrentPower,
                    RequestPower = common.RequestPower,
                    SlotLayout = new SlotLayoutDto { Input = machineParam.InputSlotCount, Output = machineParam.OutputSlotCount, Module = machineParam.ModuleSlotCount },
                };
            }

            // 発電機: PowerGenerator StateDetail
            // Generators: the PowerGenerator state detail
            var generator = block.GetStateDetail<PowerGeneratorStateDetail>(PowerGeneratorStateDetail.StateDetailKey);
            if (generator != null)
            {
                dto.Generator = new GeneratorDetailDto
                {
                    RemainingFuelTime = generator.RemainingFuelTime,
                    CurrentFuelTime = generator.CurrentFuelTime,
                    OperatingRate = generator.OperatingRate,
                };
            }

            // 採掘機: CommonMiner StateDetail + マスタ MineSettings から分間採掘数を算出
            // Miners: the CommonMiner state detail plus per-minute rates derived from master MineSettings
            var miner = block.GetStateDetail<CommonMinerBlockStateDetail>(CommonMinerBlockStateDetail.BlockStateDetailKey);
            if (miner != null && common != null && param is IMinerParam minerParam)
            {
                dto.Progress = common.ProcessingRate;
                dto.Miner = new MinerDetailDto
                {
                    CurrentPower = common.CurrentPower,
                    RequestPower = common.RequestPower,
                    MiningItems = BuildMiningItems(miner, minerParam),
                };
            }

            // ギア: GearStateDetail + マスタ GearConsumption（要求値）
            // Gears: the GearStateDetail plus master GearConsumption requirements
            var gear = block.GetStateDetail<GearStateDetail>(GearStateDetail.BlockStateDetailKey);
            if (gear != null)
            {
                var consumption = GetGearConsumption(param);
                dto.Gear = new GearDetailDto
                {
                    IsClockwise = gear.IsClockwise,
                    CurrentRpm = gear.CurrentRpm,
                    CurrentTorque = gear.CurrentTorque,
                    BaseRpm = consumption != null ? (float)consumption.BaseRpm : 0f,
                    BaseTorque = consumption != null ? (float)consumption.BaseTorque : 0f,
                };
            }

            // 液体スロット: FluidMachineInventory StateDetail（入力→出力の順で連結）
            // Fluid slots: the FluidMachineInventory state detail (inputs then outputs)
            var fluid = block.GetStateDetail<FluidMachineInventoryStateDetail>(FluidMachineInventoryStateDetail.BlockStateDetailKey);
            if (fluid != null)
            {
                AppendFluidSlots(dto.FluidSlots, fluid.InputTanks);
                AppendFluidSlots(dto.FluidSlots, fluid.OutputTanks);
            }

            ApplyNetworkCaches(dto, cache);
        }

        // ネットワーク集約キャッシュを dto に写す（未取得は null のままキー省略）
        // Copy network aggregate caches into the dto (unfetched stays null and is key-omitted)
        private static void ApplyNetworkCaches(BlockInventoryDto dto, BlockNetworkInfoCache cache)
        {
            if (cache.Electric != null)
            {
                dto.ElectricNetwork = new ElectricNetworkDto
                {
                    TotalGeneratePower = cache.Electric.TotalGeneratePower,
                    TotalRequiredPower = cache.Electric.TotalRequiredPower,
                    ConsumerCount = cache.Electric.ConsumerCount,
                    PowerRate = cache.Electric.PowerRate,
                };
            }
            if (cache.GearNetwork != null)
            {
                dto.GearNetwork = new GearNetworkDto
                {
                    TotalRequiredGearPower = cache.GearNetwork.TotalRequiredGearPower,
                    TotalGenerateGearPower = cache.GearNetwork.TotalGenerateGearPower,
                    StopReason = ToCamelCase(cache.GearNetwork.StopReason.ToString()),
                };
            }
            if (cache.FilterSplitter != null)
            {
                var directions = new List<FilterSplitterDirectionDto>();
                foreach (var d in cache.FilterSplitter.Directions)
                {
                    var itemIds = new List<int>();
                    foreach (var id in d.FilterItemIds) itemIds.Add(id.AsPrimitive());
                    directions.Add(new FilterSplitterDirectionDto { Mode = ToCamelCase(d.Mode.ToString()), FilterItemIds = itemIds });
                }
                dto.FilterSplitter = new FilterSplitterDto
                {
                    DirectionCount = cache.FilterSplitter.DirectionCount,
                    FilterSlotCountPerDirection = cache.FilterSplitter.FilterSlotCountPerDirection,
                    Directions = directions,
                };
            }
        }

        private static List<MiningItemDto> BuildMiningItems(CommonMinerBlockStateDetail miner, IMinerParam minerParam)
        {
            // uGUI MinerBlockInventoryView.cs:126-140 と同じ算出（60/Time を分間数に）
            // Same derivation as uGUI MinerBlockInventoryView.cs:126-140 (60/Time per minute)
            var result = new List<MiningItemDto>();
            var currentIds = miner.GetCurrentMiningItemIds();
            foreach (var settings in minerParam.MineSettings.items)
            {
                var itemId = MasterHolder.ItemMaster.GetItemId(settings.ItemGuid);
                if (!currentIds.Contains(itemId)) continue;
                if (settings.Time <= 0f) continue;
                result.Add(new MiningItemDto { ItemId = itemId.AsPrimitive(), ItemsPerMinute = 60f / (float)settings.Time });
            }
            return result;
        }

        private static void AppendFluidSlots(List<BlockFluidSlotDto> slots, List<Server.Util.MessagePack.FluidMessagePack> tanks)
        {
            foreach (var tank in tanks)
            {
                // 空流体は名前空文字（uGUI MachineBlockInventoryView の EmptyFluidId 分岐と同じ扱い）
                // Empty fluids get an empty name (same handling as the EmptyFluidId branch in uGUI MachineBlockInventoryView)
                var isEmpty = tank.FluidId == FluidMaster.EmptyFluidId;
                var name = isEmpty ? "" : MasterHolder.FluidMaster.GetFluidMaster(tank.FluidId).Name;
                slots.Add(new BlockFluidSlotDto { FluidId = tank.FluidId, Amount = tank.Amount, Capacity = tank.MaxCapacity, Name = name });
            }
        }

        private static Mooresmaster.Model.GearConsumptionModule.GearConsumption GetGearConsumption(object param)
        {
            // ギア消費要求値を持つ param のみ対象（GearMachine / GearMiner）
            // Only params carrying gear consumption requirements (GearMachine / GearMiner)
            return param switch
            {
                GearMachineBlockParam p => p.GearConsumption,
                GearMinerBlockParam p => p.GearConsumption,
                _ => null,
            };
        }

        private static string ToCamelCase(string value)
        {
            if (string.IsNullOrEmpty(value)) return value;
            return char.ToLowerInvariant(value[0]) + value.Substring(1);
        }
    }
}
```

実装時の確認点（すべてコンパイル/既存コード読みで確定させる。placeholder ではなく参照先の指定）:
- `FluidMessagePack` の namespace と `FluidId` の型は `moorestech_server/.../Game.Block.Interface/State/FluidMachineInventoryStateDetail.cs:39-44` が正
- `IMachineParam`/`IMinerParam`/`GearMachineBlockParam`/`GearMinerBlockParam` の namespace は uGUI ビュー（`MachineBlockInventoryView.cs`, `MinerBlockInventoryView.cs`, `GearMachineBlockInventoryView.cs`）の using が正。`GearMinerBlockParam` が `GearConsumption` を持たない場合は `GearMinerBlockInventoryView.cs` の取得方法に合わせる
- `mineSettings.items`（小文字）は実プロパティ名（`MinerBlockInventoryView.cs:128` 準拠）
- 200行を超えたら `BlockDetailDtoBuilder.cs` を `MachineDetailBuilder.cs`/`NetworkDetailBuilder.cs` 等に分割（1ディレクトリ10ファイル以内は維持）

- [ ] **Step 3: BlockInventoryTopic に組み込む**

`BlockInventoryTopic.cs` を修正。コンストラクタで `BlockNetworkInfoCache` を生成し、開ブロック変更の追跡と state イベント購読（**サーバーイベント直接購読・uGUI編集なし**）を行う:

```csharp
        private readonly BlockNetworkInfoCache _networkCache = new();
        private BlockGameObject _trackedBlock;
        private IDisposable _blockStateSubscription;

        // コンストラクタ末尾に追加
        // Append to the constructor
        _networkCache.OnUpdated += SchedulePublish;
```

`BuildJson()` の開判定後（`blockSource` 確定後）に、datastore からブロック実体を引いて追跡切替 + 詳細充填:

```csharp
            // BlockGameObject は既存公開の datastore から座標で解決する（uGUI 編集なし）
            // Resolve the BlockGameObject by position via the existing public datastore (no uGUI edits)
            if (!ClientDIContext.BlockGameObjectDataStore.TryGetBlockGameObject(blockSource.BlockPosition, out var block))
            {
                TrackBlock(null);
                return WebUiJson.Serialize(new BlockInventoryDto { Open = false });
            }
            TrackBlock(block);

            // ...既存の dto 構築とスロットコピーの後...
            BlockDetailDtoBuilder.Apply(dto, block, _networkCache);
            return WebUiJson.Serialize(dto);
```

閉時（`!open || sub == null || blockSource == null`）の return 前に `TrackBlock(null);` を追加。`TrackBlock` と公開口を追加:

```csharp
        public BlockNetworkInfoCache NetworkCache => _networkCache;

        private void TrackBlock(BlockGameObject block)
        {
            if (ReferenceEquals(_trackedBlock, block)) return;
            // 前のブロックの state イベント購読を解除する
            // Unsubscribe the previous block's state event
            _blockStateSubscription?.Dispose();
            _blockStateSubscription = null;
            _trackedBlock = block;
            if (block == null)
            {
                _networkCache.Clear();
                return;
            }

            // uGUI BlockGameObject と同じ per-block イベントタグを直接購読して再配信トリガにする
            // Directly subscribe the same per-block event tag as uGUI BlockGameObject as the republish trigger
            var eventTag = ChangeBlockStateEventPacket.CreateSpecifiedBlockEventTag(block.BlockPosInfo);
            _blockStateSubscription = ClientContext.VanillaApi.Event.SubscribeEventResponse(eventTag, _ => SchedulePublish());

            var blockType = block.BlockMasterElement.BlockType;
            // ネットワーク集約の要否は blockType で決める（spec §2-a の組み合わせ表）
            // Whether to fetch network aggregates is decided by blockType (spec §2-a combination table)
            var electric = blockType is "ElectricMachine" or "ElectricGenerator" or "ElectricMiner";
            var gear = blockType is "GearMachine" or "GearMiner" or "FuelGearGenerator" or "SimpleGearGenerator";
            var filterSplitter = blockType == "FilterSplitter";
            _networkCache.Track(block, electric, gear, filterSplitter);
        }
```

`Dispose()` に `TrackBlock(null);` を追加。using に `Client.Game.InGame.Block` / `Client.Game.InGame.Context` / `Server.Event.EventReceive` を追加。ファイルが200行を超えるため、DTO クラス群（`BlockInventoryDto`/`BlockItemSlotDto`/`BlockFluidSlotDto`）を `Game/Topics/BlockDetail/BlockInventoryDtos.cs` に移動する（namespace は `Client.WebUiHost.Game.Topics` のまま維持し、既存参照を壊さない）。

実装時の確認点: `CreateSpecifiedBlockEventTag` の引数型（`BlockPositionInfo` か `Vector3Int` か）は `Server.Event/EventReceive/ChangeBlockStateEventPacket.cs:35-38` と uGUI `BlockGameObject.cs:93` の呼び方が正。timing 注: 本購読は再配信トリガ専用で、`GetStateDetail` の実データは uGUI 側購読が `_blockStateMessagePack` を更新した後（end-of-frame の publish 時点）に読まれるため順序問題はない。

- [ ] **Step 4: コンパイルとテスト**

```bash
uloop compile --project-path ./moorestech_client
uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "WireContract"
```
Expected: ErrorCount 0 / green

- [ ] **Step 5: Commit**

```bash
git add moorestech_client/Assets/Scripts/Client.WebUiHost
git commit -m "feat(webui-host): block_inventory topic に capability 詳細充填とネットワーク取得を実装"
```

---

### Task 7: C# ResearchTopic

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Topics/ResearchTopic.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/WebUiGameBinder.cs`

**Interfaces:**
- Consumes: Task 4 の `ResearchTreeDto`/`ResearchNodeDto`
- Produces: topic `research.tree`、`ResearchTopic.ApplyNodeStates(Dictionary<Guid, ResearchNodeState>)`（Task 8 の research.complete handler が応答反映に使う公開口）

- [ ] **Step 1: ResearchTopic.cs を作成**

```csharp
using System;
using System.Collections.Generic;
using System.Threading;
using Client.Game.InGame.Context;
using Client.Game.InGame.UI.UIState;
using Client.WebUiHost.Boot;
using Client.WebUiHost.Common;
using Core.Master;
using Cysharp.Threading.Tasks;
using Game.Research;

namespace Client.WebUiHost.Game.Topics
{
    /// <summary>
    /// research.tree トピック: 研究マスタ + サーバー状態を合成して push（ResearchTree 突入時に再取得）
    /// research.tree topic: merges research master with server states and pushes (refetch on entering ResearchTree)
    /// </summary>
    public class ResearchTopic : ITopicHandler, IDisposable
    {
        public const string TopicName = "research.tree";

        private readonly WebSocketHub _hub;
        private readonly UIStateControl _uiStateControl;
        private Dictionary<Guid, ResearchNodeState> _nodeStates = new();
        private CancellationTokenSource _cts;
        private bool _disposed;

        public ResearchTopic(WebSocketHub hub, UIStateControl uiStateControl)
        {
            _hub = hub;
            _uiStateControl = uiStateControl;
            _uiStateControl.OnStateChanged += OnStateChanged;
        }

        public UniTask<string> GetSnapshotJsonAsync()
        {
            return UniTask.FromResult(BuildJson());
        }

        public void Dispose()
        {
            _disposed = true;
            _uiStateControl.OnStateChanged -= OnStateChanged;
            _cts?.Cancel();
            _cts?.Dispose();
        }

        // research.complete 応答の最新全ノード状態を反映する公開口
        // Public entry to apply the latest node states from a research.complete response
        public void ApplyNodeStates(Dictionary<Guid, ResearchNodeState> nodeStates)
        {
            _nodeStates = nodeStates;
            _hub.Publish(TopicName, BuildJson());
        }

        private void OnStateChanged(UIStateEnum state)
        {
            // ResearchTree 突入時のみサーバーから最新状態を取り直す（uGUI と同じ駆動）
            // Refetch server states only on entering ResearchTree (same trigger as uGUI)
            if (state != UIStateEnum.ResearchTree) return;
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = new CancellationTokenSource();
            RefreshAsync(_cts.Token).Forget();
        }

        private async UniTaskVoid RefreshAsync(CancellationToken ct)
        {
            var states = await ClientContext.VanillaApi.Response.GetResearchNodeStates(ct);
            if (_disposed || ct.IsCancellationRequested || states == null) return;
            ApplyNodeStates(states);
        }

        private string BuildJson()
        {
            var dto = new ResearchTreeDto { Nodes = new List<ResearchNodeDto>() };
            foreach (var master in MasterHolder.ResearchMaster.GetAllResearches())
            {
                dto.Nodes.Add(ResearchNodeDtoFactory.Create(master, _nodeStates));
            }
            return WebUiJson.Serialize(dto);
        }
    }
}
```

- [ ] **Step 2: ResearchNodeDtoFactory を作成**

`Game/Topics/ResearchNodeDtoFactory.cs` を新規作成。マスタ1件 + 状態Dict から `ResearchNodeDto` を組み立てる。**uGUI `ResearchTreeElement.cs:107-152`（報酬/解放/消費アイテムの ClearedActions 解析）と `ResearchNodeData.cs` の実装をそのまま移植する**（GameActionElement の型名・プロパティ名はこの2ファイルの記述が正）:

```csharp
using System;
using System.Collections.Generic;
using Core.Master;
using Game.Research;
using Mooresmaster.Model.ResearchModule;

namespace Client.WebUiHost.Game.Topics
{
    /// <summary>
    /// 研究マスタ + サーバー状態 → ResearchNodeDto の変換（uGUI ResearchTreeElement の解析を移植）
    /// Converts research master + server state into ResearchNodeDto (ported from uGUI ResearchTreeElement parsing)
    /// </summary>
    public static class ResearchNodeDtoFactory
    {
        public static ResearchNodeDto Create(ResearchNodeMasterElement master, Dictionary<Guid, ResearchNodeState> states)
        {
            var state = states.TryGetValue(master.ResearchNodeGuid, out var s) ? s : ResearchNodeState.UnresearchableAllReasons;
            var dto = new ResearchNodeDto
            {
                Guid = master.ResearchNodeGuid.ToString(),
                Name = master.ResearchNodeName,
                Description = master.ResearchNodeDescription,
                State = ToStateString(state),
                Position = new ResearchPositionDto { X = master.GraphViewSettings.UIPosition.x, Y = master.GraphViewSettings.UIPosition.y },
                PrevGuids = new List<string>(),
                ConsumeItems = new List<ResearchConsumeItemDto>(),
                RewardItemIds = new List<int>(),
                UnlockItemIds = new List<int>(),
            };

            foreach (var prev in master.PrevResearchNodeGuids) dto.PrevGuids.Add(prev.ToString());

            // 消費アイテム（GuidをItemIdへ変換）
            // Consume items (convert Guid to ItemId)
            foreach (var consume in master.ConsumeItems)
            {
                var itemId = MasterHolder.ItemMaster.GetItemId(consume.ItemGuid);
                dto.ConsumeItems.Add(new ResearchConsumeItemDto { ItemId = itemId.AsPrimitive(), Count = consume.ItemCount });
            }

            // 報酬/解放アイテムは ClearedActions から抽出（uGUI ResearchTreeElement.cs:107-138 と同じ分岐）
            // Rewards/unlocks come from ClearedActions (same branching as uGUI ResearchTreeElement.cs:107-138)
            AppendActionItems(dto, master);
            return dto;
        }

        private static void AppendActionItems(ResearchNodeDto dto, ResearchNodeMasterElement master)
        {
            // ClearedActions から報酬(giveItem)と解放(unlockItemRecipeView)のアイテムを抽出する
            // Extract reward (giveItem) and unlock (unlockItemRecipeView) items from ClearedActions
            foreach (var action in master.ClearedActions.items)
            {
                if (action.GameActionType == "giveItem" && action.GameActionParam is GiveItemGameActionParam give)
                {
                    foreach (var reward in give.RewardItems)
                        dto.RewardItemIds.Add(MasterHolder.ItemMaster.GetItemId(reward.ItemGuid).AsPrimitive());
                }
                if (action.GameActionType == "unlockItemRecipeView" && action.GameActionParam is UnlockItemRecipeViewGameActionParam unlock)
                {
                    foreach (var itemGuid in unlock.UnlockItemGuids)
                        dto.UnlockItemIds.Add(MasterHolder.ItemMaster.GetItemId(itemGuid).AsPrimitive());
                }
            }
        }

        private static string ToStateString(ResearchNodeState state)
        {
            return state switch
            {
                ResearchNodeState.Completed => "completed",
                ResearchNodeState.Researchable => "researchable",
                ResearchNodeState.UnresearchableNotEnoughItem => "unresearchableNotEnoughItem",
                ResearchNodeState.UnresearchableNotEnoughPreNode => "unresearchableNotEnoughPreNode",
                _ => "unresearchableAllReasons",
            };
        }
    }
}
```

実装時の確認点: `GiveItemGameActionParam`/`UnlockItemRecipeViewGameActionParam`/`GameActionType`/`ClearedActions.items` は SourceGenerator 生成名のため、**uGUI `ResearchTreeElement.cs:107-138` の実際の型名・分岐の書き方が正**。一致しなければそちらに合わせる（using も同ファイル準拠）。`ResearchNodeState` の namespace は `IResearchDataStore.cs`（`Game.Research` 想定）の実際の宣言に合わせる。

- [ ] **Step 3: Binder 登録**

`WebUiGameBinder.Bind()` の topic 登録群に追加:

```csharp
            // 研究ツリートピックを登録（表示可否は ui_state.current 側で判定）
            // Register the research-tree topic (visibility is decided by ui_state.current)
            var researchTopic = new ResearchTopic(hub, uiStateControl);
            hub.RegisterTopic(ResearchTopic.TopicName, researchTopic);
```

- [ ] **Step 4: コンパイル**

Run: `uloop compile --project-path ./moorestech_client`
Expected: ErrorCount 0

- [ ] **Step 5: Commit**

```bash
git add moorestech_client/Assets/Scripts/Client.WebUiHost
git commit -m "feat(webui-host): research.tree topic を実装（マスタ+サーバー状態の合成配信）"
```

---

### Task 8: C# ActionHandler 3種 + エラーコード契約

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Actions/ResearchActions.cs`
- Create: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Actions/FilterSplitterActions.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/WebUiGameBinder.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Tests/WebUi/WireFixtures/error_codes.json`
- Modify: `moorestech_client/Assets/Scripts/Client.Tests/WebUi/WireContractTest.cs`（エラーコード網羅テストの期待集合）

**Interfaces:**
- Consumes: Task 6 の `BlockInventoryTopic.NetworkCache.ApplyFilterSplitterSnapshot()`（`OnUpdated` 経由で再配信される）、Task 7 の `ResearchTopic.ApplyNodeStates()`
- Produces: action `research.complete`（エラー: `invalid_payload`/`invalid_guid`/`research_failed`）、`filter_splitter.set_mode`・`filter_splitter.set_filter_item`（エラー: `invalid_payload`/`block_not_open`/`invalid_direction`/`invalid_slot`/`filter_request_failed`）

- [ ] **Step 1: ResearchActions.cs を作成**

```csharp
using System;
using System.Threading;
using Client.Game.InGame.Context;
using Client.WebUiHost.Game.Topics;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace Client.WebUiHost.Game.Actions
{
    /// <summary>
    /// research.complete: 研究実行をサーバーへ送信し、応答の全ノード状態で topic を再配信する
    /// research.complete: sends a research completion to the server and republishes the topic with the response states
    /// </summary>
    public class ResearchCompleteActionHandler : IActionHandler
    {
        public string ActionType => "research.complete";

        private readonly ResearchTopic _researchTopic;

        public ResearchCompleteActionHandler(ResearchTopic researchTopic)
        {
            _researchTopic = researchTopic;
        }

        public async UniTask<ActionResult> ExecuteAsync(JObject payload)
        {
            if (payload == null) return ActionResult.Fail("invalid_payload");
            if (payload["researchGuid"] is not JValue { Type: JTokenType.String } guidValue) return ActionResult.Fail("invalid_payload");
            if (!Guid.TryParse((string)guidValue, out var researchGuid)) return ActionResult.Fail("invalid_guid");

            var response = await ClientContext.VanillaApi.Response.CompleteResearch(researchGuid, CancellationToken.None);
            if (response == null) return ActionResult.Fail("research_failed");

            // 成否に関わらず最新全ノード状態を配信し、Web を正しい状態へ収束させる
            // Publish the latest node states regardless of success so the web converges to the true state
            _researchTopic.ApplyNodeStates(response.NodeState.ToDictionary());
            return response.Success ? ActionResult.Success() : ActionResult.Fail("research_failed");
        }
    }
}
```

- [ ] **Step 2: FilterSplitterActions.cs を作成**

set_mode / set_filter_item の2ハンドラ。現在開いているブロック座標は `SubInventoryState.CurrentSubInventorySource as BlockSubInventorySource` から取得し、閉なら `block_not_open`:

```csharp
using System.Threading;
using Client.Game.InGame.Context;
using Client.Game.InGame.UI.Inventory.Main;
using Client.Game.InGame.UI.UIState.State;
using Client.Game.InGame.UI.UIState.State.SubInventory;
using Client.WebUiHost.Game.Topics;
using Core.Master;
using Cysharp.Threading.Tasks;
using Game.Block.Blocks.FilterSplitter;
using Newtonsoft.Json.Linq;
using Server.Protocol.PacketResponse;

namespace Client.WebUiHost.Game.Actions
{
    /// <summary>
    /// filter_splitter.set_mode: 開いているフィルタ分岐器のモードを明示指定で変更する（冪等）
    /// filter_splitter.set_mode: sets the open filter splitter's mode explicitly (idempotent)
    /// </summary>
    public class FilterSplitterSetModeActionHandler : IActionHandler
    {
        public string ActionType => "filter_splitter.set_mode";

        private readonly SubInventoryState _subInventoryState;
        private readonly BlockInventoryTopic _blockInventoryTopic;

        public FilterSplitterSetModeActionHandler(SubInventoryState subInventoryState, BlockInventoryTopic blockInventoryTopic)
        {
            _subInventoryState = subInventoryState;
            _blockInventoryTopic = blockInventoryTopic;
        }

        public async UniTask<ActionResult> ExecuteAsync(JObject payload)
        {
            if (payload == null) return ActionResult.Fail("invalid_payload");
            if (payload["directionIndex"] is not JValue { Value: long dirLong }) return ActionResult.Fail("invalid_direction");
            var mode = ParseMode(payload["mode"]);
            if (mode == null) return ActionResult.Fail("invalid_payload");
            if (_subInventoryState.CurrentSubInventorySource is not BlockSubInventorySource source) return ActionResult.Fail("block_not_open");

            var request = FilterSplitterStateProtocol.FilterSplitterStateRequest.CreateSetModeRequest(source.BlockPosition, (int)dirLong, mode.Value);
            var response = await ClientContext.VanillaApi.Response.SendFilterSplitterStateRequest(request, CancellationToken.None);
            if (response == null || !response.Success) return ActionResult.Fail("filter_request_failed");

            // 応答スナップショットをキャッシュへ反映し topic を再配信する（D2: state は topic 一本）
            // Apply the response snapshot to the cache and republish the topic (D2: state flows only via topics)
            _blockInventoryTopic.NetworkCache.ApplyFilterSplitterSnapshot(response);
            return ActionResult.Success();
        }

        private static FilterSplitterMode? ParseMode(JToken token)
        {
            if (token is not JValue { Type: JTokenType.String } value) return null;
            return (string)value switch
            {
                "default" => FilterSplitterMode.Default,
                "whitelist" => FilterSplitterMode.Whitelist,
                "blacklist" => FilterSplitterMode.Blacklist,
                _ => null,
            };
        }
    }

    /// <summary>
    /// filter_splitter.set_filter_item: clear=false は Grab の持ち手アイテムを設定、clear=true は解除
    /// filter_splitter.set_filter_item: clear=false assigns the grabbed item, clear=true clears the slot
    /// </summary>
    public class FilterSplitterSetFilterItemActionHandler : IActionHandler
    {
        public string ActionType => "filter_splitter.set_filter_item";

        private readonly SubInventoryState _subInventoryState;
        private readonly LocalPlayerInventoryController _controller;
        private readonly BlockInventoryTopic _blockInventoryTopic;

        public FilterSplitterSetFilterItemActionHandler(SubInventoryState subInventoryState, LocalPlayerInventoryController controller, BlockInventoryTopic blockInventoryTopic)
        {
            _subInventoryState = subInventoryState;
            _controller = controller;
            _blockInventoryTopic = blockInventoryTopic;
        }

        public async UniTask<ActionResult> ExecuteAsync(JObject payload)
        {
            if (payload == null) return ActionResult.Fail("invalid_payload");
            if (payload["directionIndex"] is not JValue { Value: long dirLong }) return ActionResult.Fail("invalid_direction");
            if (payload["slotIndex"] is not JValue { Value: long slotLong }) return ActionResult.Fail("invalid_slot");
            if (payload["clear"] is not JValue { Type: JTokenType.Boolean } clearValue) return ActionResult.Fail("invalid_payload");
            if (_subInventoryState.CurrentSubInventorySource is not BlockSubInventorySource source) return ActionResult.Fail("block_not_open");

            // uGUI FilterSplitterBlockInventoryView.cs:125-147 と同じ: 設定は Grab の現アイテム、解除は EmptyItemId
            // Same as uGUI FilterSplitterBlockInventoryView.cs:125-147: assign the grabbed item, or EmptyItemId to clear
            var itemId = (bool)clearValue.Value ? ItemMaster.EmptyItemId : _controller.GrabInventory.Id;
            var request = FilterSplitterStateProtocol.FilterSplitterStateRequest.CreateSetFilterItemRequest(source.BlockPosition, (int)dirLong, (int)slotLong, itemId);
            var response = await ClientContext.VanillaApi.Response.SendFilterSplitterStateRequest(request, CancellationToken.None);
            if (response == null || !response.Success) return ActionResult.Fail("filter_request_failed");

            _blockInventoryTopic.NetworkCache.ApplyFilterSplitterSnapshot(response);
            return ActionResult.Success();
        }
    }
}
```

（`_controller.GrabInventory.Id` の型が `ItemId` であること・`CreateSetFilterItemRequest` の引数型は `FilterSplitterBlockInventoryView.cs:137-147` が正。合わなければそこに合わせる）

- [ ] **Step 3: Binder に登録**

`WebUiGameBinder.Bind()` の action 登録群に追加:

```csharp
            hub.RegisterAction(new ResearchCompleteActionHandler(researchTopic));
            hub.RegisterAction(new FilterSplitterSetModeActionHandler(subInventoryState, blockInventoryTopic));
            hub.RegisterAction(new FilterSplitterSetFilterItemActionHandler(subInventoryState, controller, blockInventoryTopic));
```

- [ ] **Step 4: エラーコード契約を更新**

`error_codes.json` の `codes` 配列に追加: `"invalid_guid"`, `"research_failed"`, `"block_not_open"`, `"invalid_direction"`, `"filter_request_failed"`
`WireContractTest.cs` の `ErrorCodesFixtureCoversAllHandlerCodes` の期待集合にも同5コードを追加（テストがハンドラ全コード網羅を強制している）。

- [ ] **Step 5: コンパイルとテスト**

```bash
uloop compile --project-path ./moorestech_client
uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "WireContract"
```
Expected: ErrorCount 0 / green

- [ ] **Step 6: Commit**

```bash
git add moorestech_client/Assets/Scripts/Client.WebUiHost moorestech_client/Assets/Scripts/Client.Tests
git commit -m "feat(webui-host): 研究実行とフィルタ分岐器設定のActionを実装"
```

---

### Task 9: mock-host 拡張（fixtures + サーバー分岐 + テスト用エンドポイント）

**Files:**
- Modify: `moorestech_web/webui/e2e/mock-host/fixtures.ts`
- Modify: `moorestech_web/webui/e2e/mock-host/server.ts`

**Interfaces:**
- Consumes: Task 1-2 の payload 型
- Produces: `/__block?type=machine|gearMachine|generator|miner|filterSplitter|chest|tank|closed`、`/__uistate?state=ResearchTree|GameScreen`、topic `research.tree` の配信、`KNOWN_ACTIONS` に新Action 3種（apply: set_mode はモード書換、set_filter_item は itemId 書換（clear は 0）、research.complete は該当ノードを completed 化）

- [ ] **Step 1: fixtures.ts にサンプルデータを追加**

Task 3 の WireFixtures と同形状のデータを `blockMachine` / `blockGearMachine` / `blockGenerator` / `blockMiner` / `blockFilterSplitter` / `researchTree` としてexport（値はフィクスチャからコピーし、`identifier` だけ既存 fixtures の流儀に合わせる）。研究は researchable ノードを1つ含め、研究実行 e2e が状態遷移を検証できるようにする（`state: "researchable"` のノード guid `"33333333-3333-3333-3333-333333333333"` を追加した3ノード構成にする）。

- [ ] **Step 2: server.ts に分岐を追加**

- `topicData(topic)` に `research.tree` の分岐を追加（現在の `researchTreeState` 変数を返す）
- `/__block` の type マップに5種を追加（既存 chest/tank/closed パターンの拡張）
- `/__uistate?state=X` エンドポイントを追加し `ui_state.current` を差し替え+push（既存 `/__block` の subscriber push パターンを踏襲）
- `KNOWN_ACTIONS` に `"research.complete"`, `"filter_splitter.set_mode"`, `"filter_splitter.set_filter_item"` を追加
- apply ロジック: `filter_splitter.set_mode` → currentBlock の `filterSplitter.directions[directionIndex].mode` を書換えて event push。`filter_splitter.set_filter_item` → `filterItemIds[slotIndex]` を clear なら 0、それ以外は 999（mock 用の固定 grab アイテムID）に書換えて push。`research.complete` → researchTree の該当 guid ノードを `completed` に書換えて push

- [ ] **Step 3: ビルドとmock起動確認**

Run: `cd moorestech_web/webui && pnpm build && pnpm tsx e2e/mock-host/server.ts &` → `curl -s localhost:5273/__block?type=machine` → OK 応答を確認して kill
Expected: エラーなし

- [ ] **Step 4: Commit**

```bash
git add moorestech_web/webui/e2e
git commit -m "feat(webui): mock-host にブロック詳細5種と研究ツリーを追加"
```

---

### Task 10: Web capability 表示部品（details/）+ 純関数ロジック

**Files:**
- Create: `moorestech_web/webui/src/features/blockInventory/details/detailLogic.ts`
- Create: `moorestech_web/webui/src/features/blockInventory/details/detailLogic.test.ts`
- Create: `moorestech_web/webui/src/features/blockInventory/details/MachineSection.tsx`
- Create: `moorestech_web/webui/src/features/blockInventory/details/GeneratorSection.tsx`
- Create: `moorestech_web/webui/src/features/blockInventory/details/MinerSection.tsx`
- Create: `moorestech_web/webui/src/features/blockInventory/details/GearSection.tsx`
- Create: `moorestech_web/webui/src/features/blockInventory/details/NetworkSections.tsx`

**Interfaces:**
- Consumes: Task 1 の capability 型、`@/shared/ui`（ItemSlot/SlotGrid/FluidSlot/ProgressArrow）、`blockInteractionContext`
- Produces: `<MachineSection data={BlockInventoryOpen} />` 等の表示部品（全部品 props は `{ data: BlockInventoryOpen }`）。detailLogic の純関数 `computePowerRate(currentPower, requestPower)`, `splitSlotIndices(layout, total)`, `fuelRatio(remaining, current)`, `stopReasonText(reason)`

- [ ] **Step 1: 失敗するロジックテストを書く**

`detailLogic.test.ts`:

```ts
import { describe, expect, it } from "vitest";
import { computePowerRate, splitSlotIndices, fuelRatio, stopReasonText } from "./detailLogic";

describe("detailLogic", () => {
  it("computePowerRate follows the uGUI formula", () => {
    expect(computePowerRate(50, 100)).toBe(0.5);
    // RequestPower==0 は uGUI と同じく 1.0 扱い
    // RequestPower==0 counts as 1.0, same as uGUI
    expect(computePowerRate(0, 0)).toBe(1);
  });
  it("splitSlotIndices splits input→output→module in order", () => {
    expect(splitSlotIndices({ input: 2, output: 1, module: 1 }, 4)).toEqual({
      input: [0, 1], output: [2], module: [3],
    });
    // 総数不一致でも範囲外を作らない
    // Never produce out-of-range indices even when counts mismatch
    expect(splitSlotIndices({ input: 3, output: 2, module: 0 }, 4)).toEqual({
      input: [0, 1, 2], output: [3], module: [],
    });
  });
  it("fuelRatio clamps to 0..1 and handles zero denominators", () => {
    expect(fuelRatio(5, 10)).toBe(0.5);
    expect(fuelRatio(0, 0)).toBe(0);
    expect(fuelRatio(20, 10)).toBe(1);
  });
  it("stopReasonText maps reasons to uGUI wording", () => {
    expect(stopReasonText("none")).toBe("");
    expect(stopReasonText("rocked")).toBe("ロック");
    expect(stopReasonText("overRequirePower")).toBe("パワー不足");
  });
});
```

- [ ] **Step 2: 失敗確認**

Run: `pnpm vitest run src/features/blockInventory/details/detailLogic.test.ts`
Expected: FAIL（module not found）

- [ ] **Step 3: detailLogic.ts を実装**

```ts
import type { GearNetworkStopReason } from "@/bridge/payloadTypes";

// uGUI CommonMachineBlockStateDetail.PowerRate と同式（ワイヤ非送信のためWeb側算出）
// Same formula as uGUI CommonMachineBlockStateDetail.PowerRate (not on the wire; computed web-side)
export function computePowerRate(currentPower: number, requestPower: number): number {
  return requestPower === 0 ? 1 : currentPower / requestPower;
}

// itemSlots の統合indexを 入力→出力→モジュール に分割（uGUIのスロット構成順）
// Split combined itemSlots indices into input→output→module (uGUI slot ordering)
export function splitSlotIndices(
  layout: { input: number; output: number; module: number },
  total: number,
): { input: number[]; output: number[]; module: number[] } {
  const all = Array.from({ length: total }, (_, i) => i);
  const input = all.slice(0, layout.input);
  const output = all.slice(layout.input, layout.input + layout.output);
  const module = all.slice(layout.input + layout.output, layout.input + layout.output + layout.module);
  return { input, output, module };
}

export function fuelRatio(remainingFuelTime: number, currentFuelTime: number): number {
  if (currentFuelTime <= 0) return 0;
  return Math.min(1, Math.max(0, remainingFuelTime / currentFuelTime));
}

// uGUI GearEnergyTransformerUIView.GetStopReasonText と同文言
// Same wording as uGUI GearEnergyTransformerUIView.GetStopReasonText
export function stopReasonText(reason: GearNetworkStopReason): string {
  if (reason === "rocked") return "ロック";
  if (reason === "overRequirePower") return "パワー不足";
  return "";
}
```

- [ ] **Step 4: テスト成功確認**

Run: `pnpm vitest run src/features/blockInventory/details/detailLogic.test.ts` → PASS

- [ ] **Step 5: 表示部品を実装**

`MachineSection.tsx`（既存 `BlockItemGrid` は連結index前提のため、分割描画は `ItemSlot` 直使用 + `pickUpPayload`/`placePayload` を blockLogic から流用）:

```tsx
import { Group, Stack, Text } from "@mantine/core";
import type { BlockInventoryOpen } from "@/bridge/payloadTypes";
import { ItemSlot, SlotGrid, ProgressArrow, FluidSlot } from "@/shared/ui";
import { useBlockInteraction } from "../blockInteractionContext";
import { dispatchAction } from "@/bridge";
import { pickUpPayload, placePayload } from "../blockLogic";
import { computePowerRate, splitSlotIndices } from "./detailLogic";

// 機械: 入力→出力→モジュールの分割グリッド + 進捗 + 電力率（uGUI MachineBlockInventoryView 準拠）
// Machine: split input→output→module grids, progress, and power rate (mirrors uGUI MachineBlockInventoryView)
export default function MachineSection({ data }: { data: BlockInventoryOpen }) {
  const { grabCount, resolveName } = useBlockInteraction();
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
        onLeftDown={() => {
          const payload = grabCount > 0 ? placePayload(i, grabCount) : slot.itemId > 0 ? pickUpPayload(i, slot.count) : null;
          if (payload) void dispatchAction("block_inventory.move_item", payload);
        }}
      />
    );
  };

  return (
    <Stack gap="xs" data-testid="machine-section">
      <Group align="center" gap="md">
        <SlotGrid cols={Math.max(1, input.length)} testId="machine-input-slots">{input.map(slotAt)}</SlotGrid>
        <ProgressArrow value={data.progress ?? 0} />
        <SlotGrid cols={Math.max(1, output.length)} testId="machine-output-slots">{output.map(slotAt)}</SlotGrid>
      </Group>
      {module.length > 0 && <SlotGrid cols={module.length} testId="machine-module-slots">{module.map(slotAt)}</SlotGrid>}
      {data.fluidSlots.length > 0 && (
        <Group gap="xs" data-testid="machine-fluid-slots">
          {data.fluidSlots.map((f, i) => <FluidSlot key={i} fluid={f} />)}
        </Group>
      )}
      <Text size="sm" c={lacking ? "red.5" : "dark.1"} data-testid="machine-power-rate">
        電力 {Math.round(powerRate * 100)}% ({data.machine.currentPower}/{data.machine.requestPower})
      </Text>
    </Stack>
  );
}
```

`GeneratorSection.tsx`:

```tsx
import { Stack, Text, Progress } from "@mantine/core";
import type { BlockInventoryOpen } from "@/bridge/payloadTypes";
import { fuelRatio } from "./detailLogic";

// 発電機: 残燃料バー + 稼働率（uGUI GeneratorBlockInventoryView 準拠。燃料スロットはビュー側グリッドが描画）
// Generator: remaining-fuel bar and operating rate (mirrors uGUI GeneratorBlockInventoryView; fuel slots render in the view grid)
export default function GeneratorSection({ data }: { data: BlockInventoryOpen }) {
  if (!data.generator) return null;
  const ratio = fuelRatio(data.generator.remainingFuelTime, data.generator.currentFuelTime);
  return (
    <Stack gap="xs" data-testid="generator-section">
      <Progress value={ratio * 100} size="lg" color="orange" data-testid="generator-fuel-progress" />
      <Text size="sm" c="dark.1" data-testid="generator-operating-rate">
        稼働率 {Math.round(data.generator.operatingRate * 100)}%
      </Text>
    </Stack>
  );
}
```

`MinerSection.tsx`:

```tsx
import { Group, Stack, Text } from "@mantine/core";
import type { BlockInventoryOpen } from "@/bridge/payloadTypes";
import { ItemSlot, ProgressArrow } from "@/shared/ui";
import { useBlockInteraction } from "../blockInteractionContext";
import { computePowerRate } from "./detailLogic";

// 採掘機: 採掘進捗 + 電力率 + 採掘中アイテムと分間数（uGUI MinerBlockInventoryView 準拠）
// Miner: mining progress, power rate, and currently mined items with per-minute rates (mirrors uGUI MinerBlockInventoryView)
export default function MinerSection({ data }: { data: BlockInventoryOpen }) {
  const { resolveName } = useBlockInteraction();
  if (!data.miner) return null;
  const powerRate = computePowerRate(data.miner.currentPower, data.miner.requestPower);
  const lacking = powerRate < 1;
  return (
    <Stack gap="xs" data-testid="miner-section">
      <ProgressArrow value={data.progress ?? 0} />
      <Text size="sm" c={lacking ? "red.5" : "dark.1"} data-testid="miner-power-rate">
        電力 {Math.round(powerRate * 100)}% ({data.miner.currentPower}/{data.miner.requestPower})
      </Text>
      <Group gap="xs" data-testid="miner-mining-items">
        {data.miner.miningItems.map((m) => (
          <Group key={m.itemId} gap={4}>
            <ItemSlot itemId={m.itemId} name={resolveName(m.itemId)} />
            <Text size="xs" c="dark.1">{m.itemsPerMinute.toFixed(1)}/分</Text>
          </Group>
        ))}
      </Group>
    </Stack>
  );
}
```

`GearSection.tsx`:

```tsx
import { Stack, Text } from "@mantine/core";
import type { BlockInventoryOpen } from "@/bridge/payloadTypes";

// ギア: トルク/RPM の現在値と要求値（不足時赤）。uGUI SetGearText 準拠
// Gear: current vs required torque/RPM (red when lacking); mirrors uGUI SetGearText
export default function GearSection({ data }: { data: BlockInventoryOpen }) {
  if (!data.gear) return null;
  const torqueLack = data.gear.currentTorque < data.gear.baseTorque;
  const rpmLack = data.gear.currentRpm < data.gear.baseRpm;
  return (
    <Stack gap={2} data-testid="gear-section">
      <Text size="sm" c={torqueLack ? "red.5" : "dark.1"} data-testid="gear-torque">
        トルク {data.gear.currentTorque.toFixed(1)} / {data.gear.baseTorque.toFixed(1)}
      </Text>
      <Text size="sm" c={rpmLack ? "red.5" : "dark.1"} data-testid="gear-rpm">
        RPM {data.gear.currentRpm.toFixed(1)} / {data.gear.baseRpm.toFixed(1)}
      </Text>
    </Stack>
  );
}
```

`NetworkSections.tsx`（電力/ギアネットワークの2部品を1ファイルに同居。各々短小のため）:

```tsx
import { Stack, Text } from "@mantine/core";
import type { BlockInventoryOpen } from "@/bridge/payloadTypes";
import { stopReasonText } from "./detailLogic";

// 電力ネットワーク集約（uGUI ElectricNetworkInfoView 準拠。消費者0は需要なし表示）
// Electric network aggregate (mirrors uGUI ElectricNetworkInfoView; zero consumers shows a no-demand note)
export function ElectricNetworkSection({ data }: { data: BlockInventoryOpen }) {
  if (!data.electricNetwork) return null;
  const n = data.electricNetwork;
  return (
    <Stack gap={2} data-testid="electric-network-section">
      {n.consumerCount === 0 ? (
        <Text size="xs" c="dark.2">需要なし</Text>
      ) : (
        <Text size="xs" c="dark.1">
          発電 {n.totalGeneratePower.toFixed(0)} / 需要 {n.totalRequiredPower.toFixed(0)}（消費 {n.consumerCount}件, 供給率 {Math.round(n.powerRate * 100)}%）
        </Text>
      )}
    </Stack>
  );
}

// ギアネットワーク集約 + 停止理由（uGUI SetGearText の networkInfo 部準拠）
// Gear network aggregate with stop reason (mirrors the networkInfo part of uGUI SetGearText)
export function GearNetworkSection({ data }: { data: BlockInventoryOpen }) {
  if (!data.gearNetwork) return null;
  const n = data.gearNetwork;
  const reason = stopReasonText(n.stopReason);
  return (
    <Stack gap={2} data-testid="gear-network-section">
      <Text size="xs" c="dark.1">
        供給 {n.totalGenerateGearPower.toFixed(0)} / 要求 {n.totalRequiredGearPower.toFixed(0)}
      </Text>
      {reason !== "" && <Text size="xs" c="red.5" data-testid="gear-stop-reason">{reason}</Text>}
    </Stack>
  );
}
```

- [ ] **Step 6: 全体テスト**

Run: `pnpm vitest run` → 全green。`pnpm exec tsc --noEmit`（型チェック）→ エラー0

- [ ] **Step 7: Commit**

```bash
git add moorestech_web/webui/src/features/blockInventory/details
git commit -m "feat(webui): capability表示部品（機械/発電機/採掘機/ギア/ネットワーク）を追加"
```

---

### Task 11: Web ブロックビュー 6種 + レジストリ登録

**Files:**
- Create: `moorestech_web/webui/src/features/blockInventory/views/MachineInventory.tsx`
- Create: `moorestech_web/webui/src/features/blockInventory/views/GearMachineInventory.tsx`
- Create: `moorestech_web/webui/src/features/blockInventory/views/GeneratorInventory.tsx`
- Create: `moorestech_web/webui/src/features/blockInventory/views/MinerInventory.tsx`
- Create: `moorestech_web/webui/src/features/blockInventory/views/GearMinerInventory.tsx`
- Create: `moorestech_web/webui/src/features/blockInventory/views/FilterSplitterInventory.tsx`（Task 12 で実装。ここでは未登録）
- Modify: `moorestech_web/webui/src/features/blockInventory/blockLogic.ts`
- Test: `moorestech_web/webui/src/features/blockInventory/blockLogic.test.ts`

**Interfaces:**
- Consumes: Task 10 の Section 部品（contract `{ data: BlockInventoryOpen }`）
- Produces: `blockComponents` に 7 キー登録（`ElectricMachine`/`GearMachine`/`ElectricGenerator`/`FuelGearGenerator`/`SimpleGearGenerator`/`ElectricMiner`/`GearMiner`）

- [ ] **Step 1: 失敗するレジストリテストを追加**

`blockLogic.test.ts` に追記:

```ts
it.each([
  "ElectricMachine", "GearMachine", "ElectricGenerator",
  "FuelGearGenerator", "SimpleGearGenerator", "ElectricMiner", "GearMiner",
])("resolves a dedicated view for %s", (blockType) => {
  expect(resolveBlockComponent(blockType)).not.toBe(GenericBlockInventory);
});
```

（`GenericBlockInventory` の import を test に追加）

- [ ] **Step 2: 失敗確認**

Run: `pnpm vitest run src/features/blockInventory/blockLogic.test.ts` → FAIL（全typeがGenericへフォールバック）

- [ ] **Step 3: ビューを実装**

`views/MachineInventory.tsx`:

```tsx
import { Stack } from "@mantine/core";
import type { BlockInventoryOpen } from "@/bridge/payloadTypes";
import MachineSection from "../details/MachineSection";
import { ElectricNetworkSection } from "../details/NetworkSections";

// 電気機械: 機械セクション + 電力ネットワーク（FEAT-BLK-3）
// Electric machine: machine section plus electric network (FEAT-BLK-3)
export default function MachineInventory({ data }: { data: BlockInventoryOpen }) {
  return (
    <Stack gap="sm">
      <MachineSection data={data} />
      <ElectricNetworkSection data={data} />
    </Stack>
  );
}
```

`views/GearMachineInventory.tsx`:

```tsx
import { Stack } from "@mantine/core";
import type { BlockInventoryOpen } from "@/bridge/payloadTypes";
import MachineSection from "../details/MachineSection";
import GearSection from "../details/GearSection";
import { GearNetworkSection } from "../details/NetworkSections";

// ギア機械: 機械セクション + ギア + ギアネットワーク（FEAT-BLK-5、capability合成）
// Gear machine: machine section plus gear and gear network (FEAT-BLK-5, capability composition)
export default function GearMachineInventory({ data }: { data: BlockInventoryOpen }) {
  return (
    <Stack gap="sm">
      <MachineSection data={data} />
      <GearSection data={data} />
      <GearNetworkSection data={data} />
    </Stack>
  );
}
```

`views/GeneratorInventory.tsx`（燃料スロットグリッドは既存 `BlockItemGrid` を流用。ギア発電機と電気発電機の両capabilityに対応）:

```tsx
import { Stack } from "@mantine/core";
import type { BlockInventoryOpen } from "@/bridge/payloadTypes";
import BlockItemGrid from "../BlockItemGrid";
import GeneratorSection from "../details/GeneratorSection";
import GearSection from "../details/GearSection";
import { ElectricNetworkSection, GearNetworkSection } from "../details/NetworkSections";

// 発電機: 燃料スロット + 残燃料/稼働率 + 電力 or ギアのネットワーク（FEAT-BLK-2）
// Generator: fuel slots, fuel/operating rate, and electric or gear network (FEAT-BLK-2)
export default function GeneratorInventory({ data }: { data: BlockInventoryOpen }) {
  return (
    <Stack gap="sm">
      <BlockItemGrid slots={data.itemSlots} />
      <GeneratorSection data={data} />
      <GearSection data={data} />
      <ElectricNetworkSection data={data} />
      <GearNetworkSection data={data} />
    </Stack>
  );
}
```

`views/MinerInventory.tsx`:

```tsx
import { Stack } from "@mantine/core";
import type { BlockInventoryOpen } from "@/bridge/payloadTypes";
import BlockItemGrid from "../BlockItemGrid";
import MinerSection from "../details/MinerSection";
import { ElectricNetworkSection } from "../details/NetworkSections";

// 電動採掘機: 出力スロット + 採掘進捗/電力/採掘中アイテム + 電力ネットワーク（FEAT-BLK-4）
// Electric miner: output slots, mining progress/power/current items, electric network (FEAT-BLK-4)
export default function MinerInventory({ data }: { data: BlockInventoryOpen }) {
  return (
    <Stack gap="sm">
      <BlockItemGrid slots={data.itemSlots} />
      <MinerSection data={data} />
      <ElectricNetworkSection data={data} />
    </Stack>
  );
}
```

`views/GearMinerInventory.tsx`:

```tsx
import { Stack } from "@mantine/core";
import type { BlockInventoryOpen } from "@/bridge/payloadTypes";
import BlockItemGrid from "../BlockItemGrid";
import MinerSection from "../details/MinerSection";
import GearSection from "../details/GearSection";
import { GearNetworkSection } from "../details/NetworkSections";

// ギア採掘機: 採掘機セクション + ギア + ギアネットワーク（FEAT-BLK-4 のギア変種）
// Gear miner: miner section plus gear and gear network (the gear variant of FEAT-BLK-4)
export default function GearMinerInventory({ data }: { data: BlockInventoryOpen }) {
  return (
    <Stack gap="sm">
      <BlockItemGrid slots={data.itemSlots} />
      <MinerSection data={data} />
      <GearSection data={data} />
      <GearNetworkSection data={data} />
    </Stack>
  );
}
```

`blockLogic.ts` の `blockComponents` を更新:

```ts
export const blockComponents: Record<string, BlockInventoryComponent> = {
  Chest: ChestInventory,
  ElectricMachine: MachineInventory,
  GearMachine: GearMachineInventory,
  ElectricGenerator: GeneratorInventory,
  FuelGearGenerator: GeneratorInventory,
  SimpleGearGenerator: GeneratorInventory,
  ElectricMiner: MinerInventory,
  GearMiner: GearMinerInventory,
};
```

（`BlockItemGrid` の props が `slots` でない場合は `ChestInventory.tsx` の使い方に合わせる。1ディレクトリ10ファイル制約: blockInventory 直下が超える場合は既存 `ChestInventory.tsx`/`TankInventory.tsx`/`GenericBlockInventory.tsx` も `views/` へ移動し import を更新する）

- [ ] **Step 4: テスト成功確認**

Run: `pnpm vitest run` → 全green。`pnpm exec tsc --noEmit` → エラー0

- [ ] **Step 5: Commit**

```bash
git add moorestech_web/webui/src/features/blockInventory
git commit -m "feat(webui): 機械/発電機/採掘機/ギア機械のブロックビューを追加しレジストリ登録"
```

---

### Task 12: FilterSplitter ビュー + 操作Action配線

**Files:**
- Create: `moorestech_web/webui/src/features/blockInventory/views/FilterSplitterInventory.tsx`
- Create: `moorestech_web/webui/src/features/blockInventory/views/filterSplitterLogic.ts`
- Test: `moorestech_web/webui/src/features/blockInventory/views/filterSplitterLogic.test.ts`
- Modify: `moorestech_web/webui/src/features/blockInventory/blockLogic.ts`（`FilterSplitter` キー登録）

**Interfaces:**
- Consumes: Task 2 の Action 契約、Task 1 の `FilterSplitterData`
- Produces: `nextMode(mode)`（default→whitelist→blacklist→default 循環の純関数）、`FilterSplitter` レジストリ登録

- [ ] **Step 1: 失敗するロジックテスト**

`filterSplitterLogic.test.ts`:

```ts
import { describe, expect, it } from "vitest";
import { nextMode } from "./filterSplitterLogic";

describe("nextMode", () => {
  it("cycles default→whitelist→blacklist→default", () => {
    expect(nextMode("default")).toBe("whitelist");
    expect(nextMode("whitelist")).toBe("blacklist");
    expect(nextMode("blacklist")).toBe("default");
  });
});
```

Run: `pnpm vitest run src/features/blockInventory/views/filterSplitterLogic.test.ts` → FAIL

- [ ] **Step 2: 実装**

`filterSplitterLogic.ts`:

```ts
import type { FilterSplitterMode } from "@/bridge/payloadTypes";

// uGUI FilterSplitterDirectionColumnView.NextMode と同じ循環。送信は明示モード指定（冪等・spec D2系）
// Same cycle as uGUI FilterSplitterDirectionColumnView.NextMode; sends explicit modes (idempotent, spec D2 family)
export function nextMode(mode: FilterSplitterMode): FilterSplitterMode {
  if (mode === "default") return "whitelist";
  if (mode === "whitelist") return "blacklist";
  return "default";
}

export const modeLabel: Record<FilterSplitterMode, string> = {
  default: "デフォルト",
  whitelist: "ホワイトリスト",
  blacklist: "ブラックリスト",
};
```

`FilterSplitterInventory.tsx`:

```tsx
import { Button, Group, Stack, Text } from "@mantine/core";
import type { BlockInventoryOpen } from "@/bridge/payloadTypes";
import { ItemSlot } from "@/shared/ui";
import { dispatchAction } from "@/bridge";
import { useBlockInteraction } from "../blockInteractionContext";
import { nextMode, modeLabel } from "./filterSplitterLogic";

// フィルタ分岐器: 方向ごとカラム + モード切替 + フィルタスロット左=設定/右=解除（FEAT-BLK-8）
// Filter splitter: per-direction columns, mode toggle, filter slots left=assign right=clear (FEAT-BLK-8)
export default function FilterSplitterInventory({ data }: { data: BlockInventoryOpen }) {
  const { resolveName } = useBlockInteraction();
  if (!data.filterSplitter) return null;
  return (
    <Group align="flex-start" gap="md" data-testid="filter-splitter">
      {data.filterSplitter.directions.map((direction, dirIndex) => (
        <Stack key={dirIndex} gap="xs" data-testid={`filter-direction-${dirIndex}`}>
          <Text size="sm" c="dark.1">出力 {dirIndex + 1}</Text>
          <Button
            size="compact-sm"
            variant="default"
            data-testid={`filter-mode-${dirIndex}`}
            onClick={() => void dispatchAction("filter_splitter.set_mode", { directionIndex: dirIndex, mode: nextMode(direction.mode) })}
          >
            {modeLabel[direction.mode]}
          </Button>
          <Group gap={4}>
            {direction.filterItemIds.map((itemId, slotIndex) => (
              <ItemSlot
                key={slotIndex}
                itemId={itemId}
                name={resolveName(itemId)}
                onLeftDown={() => void dispatchAction("filter_splitter.set_filter_item", { directionIndex: dirIndex, slotIndex, clear: false })}
                onRightDown={() => void dispatchAction("filter_splitter.set_filter_item", { directionIndex: dirIndex, slotIndex, clear: true })}
              />
            ))}
          </Group>
        </Stack>
      ))}
    </Group>
  );
}
```

`blockLogic.ts` に `FilterSplitter: FilterSplitterInventory,` を登録。

- [ ] **Step 3: テスト**

Run: `pnpm vitest run` → 全green

- [ ] **Step 4: Commit**

```bash
git add moorestech_web/webui/src/features/blockInventory
git commit -m "feat(webui): フィルタ分岐器ビューとモード/フィルタ設定Action配線を実装"
```

---

### Task 13: 研究ツリー Web 実装

**Files:**
- Create: `moorestech_web/webui/src/features/research/researchLogic.ts`
- Create: `moorestech_web/webui/src/features/research/researchLogic.test.ts`
- Create: `moorestech_web/webui/src/features/research/ResearchNodeCard.tsx`
- Create: `moorestech_web/webui/src/features/research/ResearchTreePanel.tsx`
- Create: `moorestech_web/webui/src/features/research/style.module.css`
- Create: `moorestech_web/webui/src/features/research/index.ts`
- Modify: `moorestech_web/webui/src/app/uiScreenRouting.ts`
- Modify: `moorestech_web/webui/src/app/App.tsx`
- Modify: `moorestech_web/webui/src/app/activeLayer.ts`（+ `activeLayer.test.ts`）

**Interfaces:**
- Consumes: Task 2 の `ResearchTreeData`/`ResearchNodeData`、`Topics.researchTree`、Action `research.complete`、既存 `Topics.uiState`/`Topics.inventory`
- Produces: `screenForUiState("ResearchTree") === "researchTree"`、`ActiveLayer` に `"research"` 追加。researchLogic 純関数: `computeCanvasBounds(nodes)`（padding 200）、`lineBetween(from, to)`（{length, angleDeg, x, y}）、`buildOwnedCounts(slots)`、`hasEnoughItems(node, owned)`

- [ ] **Step 1: 失敗するロジックテスト**

`researchLogic.test.ts`:

```ts
import { describe, expect, it } from "vitest";
import { computeCanvasBounds, lineBetween, hasEnoughItems } from "./researchLogic";
import type { ResearchNodeData } from "@/bridge/payloadTypes";

const node = (guid: string, x: number, y: number, extra?: Partial<ResearchNodeData>): ResearchNodeData => ({
  guid, name: guid, description: "", state: "researchable",
  position: { x, y }, prevGuids: [], consumeItems: [], rewardItemIds: [], unlockItemIds: [], ...extra,
});

describe("computeCanvasBounds", () => {
  it("wraps all node positions with padding 200 (uGUI TreeViewAdjuster parity)", () => {
    const b = computeCanvasBounds([node("a", 0, 0), node("b", 300, -120)]);
    // 最小X-200..最大X+200、Y同様。offset はノード座標を正の描画座標へ写す
    // minX-200..maxX+200 and likewise for Y; the offset maps node coords to positive drawing coords
    expect(b).toEqual({ width: 700, height: 520, offsetX: 200, offsetY: 320 });
  });
  it("handles empty nodes", () => {
    expect(computeCanvasBounds([])).toEqual({ width: 400, height: 400, offsetX: 200, offsetY: 200 });
  });
});

describe("lineBetween", () => {
  it("computes length and angle from child to parent", () => {
    const line = lineBetween({ x: 0, y: 0 }, { x: 100, y: 0 });
    expect(line.length).toBeCloseTo(100);
    expect(line.angleDeg).toBeCloseTo(0);
    const diag = lineBetween({ x: 0, y: 0 }, { x: 0, y: 100 });
    expect(diag.angleDeg).toBeCloseTo(90);
  });
});

describe("hasEnoughItems", () => {
  it("checks owned counts against consume items", () => {
    const n = node("a", 0, 0, { consumeItems: [{ itemId: 1, count: 3 }] });
    expect(hasEnoughItems(n, new Map([[1, 3]]))).toBe(true);
    expect(hasEnoughItems(n, new Map([[1, 2]]))).toBe(false);
  });
});
```

Run: `pnpm vitest run src/features/research/researchLogic.test.ts` → FAIL

- [ ] **Step 2: researchLogic.ts を実装**

```ts
import type { ResearchNodeData, SlotData } from "@/bridge/payloadTypes";

// uGUI TreeViewAdjuster.AdjustParentSize と同じ padding=200 の包含キャンバス
// Same padding=200 wrapping canvas as uGUI TreeViewAdjuster.AdjustParentSize
export const CANVAS_PADDING = 200;

export type CanvasBounds = { width: number; height: number; offsetX: number; offsetY: number };

export function computeCanvasBounds(nodes: ResearchNodeData[]): CanvasBounds {
  if (nodes.length === 0) {
    return { width: CANVAS_PADDING * 2, height: CANVAS_PADDING * 2, offsetX: CANVAS_PADDING, offsetY: CANVAS_PADDING };
  }
  const xs = nodes.map((n) => n.position.x);
  const ys = nodes.map((n) => n.position.y);
  const minX = Math.min(...xs);
  const maxX = Math.max(...xs);
  const minY = Math.min(...ys);
  const maxY = Math.max(...ys);
  return {
    width: maxX - minX + CANVAS_PADDING * 2,
    height: maxY - minY + CANVAS_PADDING * 2,
    offsetX: CANVAS_PADDING - minX,
    offsetY: CANVAS_PADDING - minY,
  };
}

// uGUI ResearchTreeElement.CreateConnect と同じ「距離 + Atan2角度の棒」モデル
// Same "length + Atan2-angle bar" model as uGUI ResearchTreeElement.CreateConnect
export type Line = { x: number; y: number; length: number; angleDeg: number };

export function lineBetween(from: { x: number; y: number }, to: { x: number; y: number }): Line {
  const dx = to.x - from.x;
  const dy = to.y - from.y;
  return { x: from.x, y: from.y, length: Math.hypot(dx, dy), angleDeg: (Math.atan2(dy, dx) * 180) / Math.PI };
}

// inventory topic の全スロットから itemId→所持数の集計を作る（craftビューと同型）
// Aggregate itemId→owned count from every inventory topic slot (same shape as the craft view)
export function buildOwnedCounts(slots: SlotData[]): Map<number, number> {
  const owned = new Map<number, number>();
  for (const slot of slots) {
    if (slot.itemId <= 0) continue;
    owned.set(slot.itemId, (owned.get(slot.itemId) ?? 0) + slot.count);
  }
  return owned;
}

export function hasEnoughItems(node: ResearchNodeData, owned: Map<number, number>): boolean {
  return node.consumeItems.every((c) => (owned.get(c.itemId) ?? 0) >= c.count);
}
```

- [ ] **Step 3: テスト成功確認**

Run: `pnpm vitest run src/features/research/researchLogic.test.ts` → PASS

- [ ] **Step 4: ノードカードとパネルを実装**

`ResearchNodeCard.tsx`:

```tsx
import { Button, Paper, Stack, Text, Tooltip, Group } from "@mantine/core";
import type { ResearchNodeData } from "@/bridge/payloadTypes";
import { ItemSlot } from "@/shared/ui";
import { dispatchAction } from "@/bridge";
import styles from "./style.module.css";

type Props = {
  node: ResearchNodeData;
  left: number;
  top: number;
  affordable: boolean;
  resolveName: (itemId: number) => string | undefined;
};

// 研究ノード1枚。uGUI ResearchTreeElement 準拠（名前/説明/消費・報酬アイコン/研究ボタン/完了オーバーレイ）
// One research node; mirrors uGUI ResearchTreeElement (name, description, consume/reward icons, research button, completed overlay)
export default function ResearchNodeCard({ node, left, top, affordable, resolveName }: Props) {
  const completed = node.state === "completed";
  const researchable = node.state === "researchable";
  return (
    <Paper
      withBorder
      p="xs"
      className={completed ? styles.nodeCompleted : styles.node}
      style={{ left, top }}
      data-testid={`research-node-${node.guid}`}
    >
      <Stack gap={4}>
        <Text size="sm" fw={600}>{node.name}</Text>
        <Text size="xs" c="dark.2" lineClamp={2}>{node.description}</Text>
        {node.consumeItems.length > 0 && (
          <Group gap={4}>
            {node.consumeItems.map((c) => (
              <Tooltip key={c.itemId} label={`${resolveName(c.itemId) ?? c.itemId} x${c.count}`}>
                <div>
                  <ItemSlot itemId={c.itemId} count={c.count} selected={affordable} />
                </div>
              </Tooltip>
            ))}
          </Group>
        )}
        {node.rewardItemIds.length + node.unlockItemIds.length > 0 && (
          <Group gap={4}>
            {[...node.rewardItemIds, ...node.unlockItemIds].map((id) => (
              <ItemSlot key={id} itemId={id} name={resolveName(id)} />
            ))}
          </Group>
        )}
        <Button
          size="compact-xs"
          disabled={!researchable}
          data-testid={`research-button-${node.guid}`}
          onClick={() => void dispatchAction("research.complete", { researchGuid: node.guid })}
        >
          {completed ? "研究済み" : "研究"}
        </Button>
      </Stack>
    </Paper>
  );
}
```

`ResearchTreePanel.tsx`:

```tsx
import { useMemo } from "react";
import { Box, ScrollArea, Title } from "@mantine/core";
import { useTopic, Topics } from "@/bridge";
import { useItemMaster } from "@/bridge/useItemMaster";
import { computeCanvasBounds, lineBetween, buildOwnedCounts, hasEnoughItems } from "./researchLogic";
import ResearchNodeCard from "./ResearchNodeCard";
import styles from "./style.module.css";

// 研究ツリー全画面パネル。表示可否は App.tsx の uiState ルーティングが決める
// Full-screen research tree panel; visibility is decided by the App.tsx uiState routing
export default function ResearchTreePanel() {
  const tree = useTopic(Topics.researchTree);
  const inventory = useTopic(Topics.inventory);
  const itemMaster = useItemMaster();
  const nodes = tree?.nodes ?? [];

  const bounds = useMemo(() => computeCanvasBounds(nodes), [nodes]);
  const byGuid = useMemo(() => new Map(nodes.map((n) => [n.guid, n])), [nodes]);
  const owned = useMemo(
    () => buildOwnedCounts([...(inventory?.mainSlots ?? []), ...(inventory?.hotbarSlots ?? [])]),
    [inventory],
  );
  const resolveName = (itemId: number) => itemMaster?.get(itemId)?.name;

  return (
    <Box className={styles.panel} data-testid="research-tree">
      <Title order={2} size="h4" p="sm">研究ツリー</Title>
      <ScrollArea className={styles.scroll} type="auto">
        <div className={styles.canvas} style={{ width: bounds.width, height: bounds.height }}>
          {/* 接続線: 子ノード → 前提ノードへ距離+角度の棒を引く（最背面） */}
          {/* Connection lines: length+angle bars from child to prerequisite (behind nodes) */}
          {nodes.flatMap((node) =>
            node.prevGuids.map((prevGuid) => {
              const prev = byGuid.get(prevGuid);
              if (!prev) return null;
              const line = lineBetween(
                { x: node.position.x + bounds.offsetX, y: node.position.y + bounds.offsetY },
                { x: prev.position.x + bounds.offsetX, y: prev.position.y + bounds.offsetY },
              );
              return (
                <div
                  key={`${node.guid}-${prevGuid}`}
                  className={styles.line}
                  style={{ left: line.x, top: line.y, width: line.length, transform: `rotate(${line.angleDeg}deg)` }}
                />
              );
            }),
          )}
          {nodes.map((node) => (
            <ResearchNodeCard
              key={node.guid}
              node={node}
              left={node.position.x + bounds.offsetX}
              top={node.position.y + bounds.offsetY}
              affordable={hasEnoughItems(node, owned)}
              resolveName={resolveName}
            />
          ))}
        </div>
      </ScrollArea>
    </Box>
  );
}
```

`style.module.css`:

```css
/* 研究ツリー: 全画面固定 + 内部スクロールキャンバス。z-index はモーダル(依存: ModalHost)より下 */
/* Research tree: full-screen fixed with an inner scroll canvas; z-index stays below the modal host */
.panel {
  position: fixed;
  inset: 0;
  z-index: 30;
  background: var(--mantine-color-dark-8);
  display: flex;
  flex-direction: column;
}
.scroll { flex: 1; }
.canvas { position: relative; }
.node, .nodeCompleted {
  position: absolute;
  width: 200px;
  transform: translate(-50%, -50%);
}
.nodeCompleted { outline: 2px solid var(--mantine-color-green-6); }
.line {
  position: absolute;
  height: 3px;
  background: var(--mantine-color-dark-4);
  transform-origin: 0 50%;
}
```

`index.ts`: `export { default as ResearchTreePanel } from "./ResearchTreePanel";`

（`useItemMaster` の実 API（Map か関数か）は `src/bridge/useItemMaster.ts` の現物に合わせる。`useTopic(Topics.inventory)` は research 表示中のみマウントされるため参照カウント購読で問題ない）

- [ ] **Step 5: ルーティングと入力排他を配線**

`uiScreenRouting.ts`:

```ts
export type UiScreen = "none" | "playerInventory" | "subInventory" | "researchTree";

export function screenForUiState(state: string | null): UiScreen {
  if (state === "PlayerInventory") return "playerInventory";
  if (state === "SubInventory") return "subInventory";
  if (state === "ResearchTree") return "researchTree";
  return "none";
}
```

`App.tsx`: import に `ResearchTreePanel` を追加し、オーバーレイ群（`<BlockInventoryPanel />` の前）に `{screen === "researchTree" && <ResearchTreePanel />}` を追加。

`activeLayer.ts`:

```ts
export type ActiveLayer = "modal" | "blockInventory" | "research" | "game";

export function deriveActiveLayer(input: { modalOpen: boolean; blockInventoryOpen: boolean; researchOpen: boolean }): ActiveLayer {
  if (input.modalOpen) return "modal";
  if (input.blockInventoryOpen) return "blockInventory";
  if (input.researchOpen) return "research";
  return "game";
}

export function readActiveLayer(): ActiveLayer {
  const modal = readTopic(Topics.modal);
  const block = readTopic(Topics.blockInventory);
  const uiState = readTopic(Topics.uiState);
  return deriveActiveLayer({
    modalOpen: modal?.modal != null,
    blockInventoryOpen: block?.open === true,
    researchOpen: uiState?.state === "ResearchTree",
  });
}
```

`activeLayer.test.ts` の `deriveActiveLayer` 呼び出しに `researchOpen: false` を追加し、research 優先順のテストを1件追加:

```ts
it("research layer sits between blockInventory and game", () => {
  expect(deriveActiveLayer({ modalOpen: false, blockInventoryOpen: false, researchOpen: true })).toBe("research");
  expect(deriveActiveLayer({ modalOpen: true, blockInventoryOpen: false, researchOpen: true })).toBe("modal");
});
```

- [ ] **Step 6: テスト**

Run: `pnpm vitest run && pnpm exec tsc --noEmit` → 全green

- [ ] **Step 7: Commit**

```bash
git add moorestech_web/webui/src
git commit -m "feat(webui): 研究ツリーパネルを実装（UIPosition配置+接続線+研究実行）"
```

---

### Task 14: Playwright e2e + 全体green確認 + ドキュメント更新

**Files:**
- Create: `moorestech_web/webui/e2e/blockDetails.spec.ts`
- Create: `moorestech_web/webui/e2e/filterSplitter.spec.ts`
- Create: `moorestech_web/webui/e2e/research.spec.ts`
- Modify: `docs/webui/TODO.md`
- Modify: `docs/superpowers/specs/2026-07-06-webui-block-research-ui-design.md`（research.tree の open union → uiState 導出への変更を反映）

**Interfaces:**
- Consumes: Task 9 の mock エンドポイント、Task 10-13 の data-testid

- [ ] **Step 1: blockDetails.spec.ts**

既存 `blockInventory.spec.ts` のパターン（afterEach で closed リセット）を踏襲:

```ts
import { test, expect } from "@playwright/test";

test.afterEach(async ({ page }) => {
  await page.request.get("/__block?type=closed");
});

const cases = [
  { type: "machine", testId: "machine-section" },
  { type: "gearMachine", testId: "gear-section" },
  { type: "generator", testId: "generator-section" },
  { type: "miner", testId: "miner-section" },
  { type: "filterSplitter", testId: "filter-splitter" },
] as const;

for (const { type, testId } of cases) {
  test(`renders ${type} detail section`, async ({ page }) => {
    await page.request.get(`/__block?type=${type}`);
    await page.goto("/");
    await expect(page.getByTestId("block-inventory")).toBeVisible();
    await expect(page.getByTestId(testId)).toBeVisible();
  });
}

test("gear machine shows torque/rpm and network info", async ({ page }) => {
  await page.request.get("/__block?type=gearMachine");
  await page.goto("/");
  await expect(page.getByTestId("gear-torque")).toContainText("トルク");
  await expect(page.getByTestId("gear-network-section")).toBeVisible();
});
```

- [ ] **Step 2: filterSplitter.spec.ts**

```ts
import { test, expect } from "@playwright/test";

test.afterEach(async ({ page }) => {
  await page.request.get("/__block?type=closed");
});

test("mode button sends explicit next mode", async ({ page }) => {
  await page.request.get("/__block?type=filterSplitter");
  await page.goto("/");
  // fixture の direction 0 は whitelist → 次は blacklist を明示送信する
  // Direction 0 in the fixture is whitelist, so the explicit next mode sent is blacklist
  await page.getByTestId("filter-mode-0").click();
  await expect
    .poll(async () => {
      const res = await page.request.get("/__actions");
      const actions = (await res.json()) as { type: string; payload: unknown }[];
      return actions.find((a) => a.type === "filter_splitter.set_mode")?.payload;
    })
    .toEqual({ directionIndex: 0, mode: "blacklist" });
});

test("right click on a filter slot sends clear", async ({ page }) => {
  await page.request.get("/__block?type=filterSplitter");
  await page.goto("/");
  await page.getByTestId("filter-direction-0").locator("[data-testid^='item-slot']").first().click({ button: "right" });
  await expect
    .poll(async () => {
      const res = await page.request.get("/__actions");
      const actions = (await res.json()) as { type: string; payload: unknown }[];
      return actions.find((a) => a.type === "filter_splitter.set_filter_item")?.payload;
    })
    .toEqual({ directionIndex: 0, slotIndex: 0, clear: true });
});
```

（ItemSlot の data-testid 実値は `src/shared/ui/ItemSlot/index.tsx` の現物に合わせる。`/__actions` の応答形式は既存 `blockInventory.spec.ts:35-43` が正）

- [ ] **Step 3: research.spec.ts**

```ts
import { test, expect } from "@playwright/test";

test.afterEach(async ({ page }) => {
  await page.request.get("/__uistate?state=GameScreen");
});

test("research tree renders nodes and lines when uiState enters ResearchTree", async ({ page }) => {
  await page.request.get("/__uistate?state=ResearchTree");
  await page.goto("/");
  await expect(page.getByTestId("research-tree")).toBeVisible();
  await expect(page.getByTestId("research-node-11111111-1111-1111-1111-111111111111")).toBeVisible();
});

test("research button sends research.complete and node becomes completed", async ({ page }) => {
  await page.request.get("/__uistate?state=ResearchTree");
  await page.goto("/");
  const researchableGuid = "33333333-3333-3333-3333-333333333333";
  await page.getByTestId(`research-button-${researchableGuid}`).click();
  await expect
    .poll(async () => {
      const res = await page.request.get("/__actions");
      const actions = (await res.json()) as { type: string; payload: unknown }[];
      return actions.find((a) => a.type === "research.complete")?.payload;
    })
    .toEqual({ researchGuid: researchableGuid });
  // mock が completed へ書換えて push → ボタンが研究済みに変わる
  // The mock rewrites the node to completed and pushes; the button flips to the completed label
  await expect(page.getByTestId(`research-button-${researchableGuid}`)).toContainText("研究済み");
});
```

- [ ] **Step 4: 全テスト実行**

```bash
cd moorestech_web/webui && pnpm vitest run && pnpm exec playwright test
uloop compile --project-path ./moorestech_client
uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "WireContract"
```
Expected: すべて green / ErrorCount 0

- [ ] **Step 5: 並行セッション変更の取り込み確認（rebase）**

別セッションが uGUI UIState ↔ Web 同期を変更している。マージ前に必ず:

```bash
git fetch origin && git log --oneline HEAD..origin/web-ui -- moorestech_web moorestech_client/Assets/Scripts/Client.WebUiHost
```

差分があれば rebase し、特に `uiScreenRouting.ts` / `App.tsx` / `activeLayer.ts` / mock-host の uiState 系の衝突を解消してから Step 4 の全テストを再実行する。

- [ ] **Step 6: ドキュメント更新**

- `docs/webui/TODO.md`: 「実装済み（機能）」に BLK-2/3/4/5/8・RES-1 を追記し、残タスク §2 から該当行を消し込み。最終更新日を更新

- [ ] **Step 7: Commit**

```bash
git add moorestech_web/webui/e2e docs/webui/TODO.md
git commit -m "test(webui): ブロック詳細/フィルタ分岐器/研究のe2eを追加しドキュメントを更新"
```
