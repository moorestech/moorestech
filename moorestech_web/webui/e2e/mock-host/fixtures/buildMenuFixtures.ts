import type { BuildMenuData } from "../../../src/bridge/contract/payloadTypes";
import { blockIconUrl } from "../../../src/bridge/transport/httpEndpoints";
import {
  CARGO_TRAIN_CAR_GUID,
  WIRE_CONNECT_TOOL_GUID,
} from "./contentLocalizationFixtures";

// 設置対象idはUnityと同じくGuid文字列。testid・アクション照合でspecが参照するためここを唯一の定義とする
// Placement target ids are GUID strings as in Unity; specs read them for testids and action matching, so this is their single definition
export const buildMenuEntryIds = {
  woodChest: "53000000-0000-4000-8000-000000000001",
  ironChest: "53000000-0000-4000-8000-000000000002",
  beltConveyor: "53000000-0000-4000-8000-000000000003",
  rail: "53000000-0000-4000-8000-000000000004",
  cargoCar: CARGO_TRAIN_CAR_GUID,
  wireConnectTool: WIRE_CONNECT_TOOL_GUID,
  blueprintCopy: "58000000-0000-4000-8000-000000000001",
  starterBaseBlueprint: "3f6a9c1e-8b2d-4f7a-9e3c-1a2b3c4d5e6f",
} as const;

export const buildMenuCategoryIds = {
  logistics: "51000000-0000-4000-8000-000000000001",
  transport: "51000000-0000-4000-8000-000000000002",
  blueprint: "51000000-0000-4000-8000-000000000003",
  building: "51000000-0000-4000-8000-000000000004",
  mining: "51000000-0000-4000-8000-000000000005",
  production: "51000000-0000-4000-8000-000000000006",
  mechanicalPower: "51000000-0000-4000-8000-000000000007",
  electricity: "51000000-0000-4000-8000-000000000008",
  fluid: "51000000-0000-4000-8000-000000000009",
  tool: "51000000-0000-4000-8000-000000000010",
  buildingMaterial: "51000000-0000-4000-8000-000000000011",
} as const;

export const buildMenuSubCategoryIds = {
  chest: "52000000-0000-4000-8000-000000000001",
  conveyor: "52000000-0000-4000-8000-000000000002",
  rail: "52000000-0000-4000-8000-000000000003",
  car: "52000000-0000-4000-8000-000000000004",
  saved: "52000000-0000-4000-8000-000000000005",
  foundation: "52000000-0000-4000-8000-000000000006",
  miner: "52000000-0000-4000-8000-000000000007",
  primitiveCraft: "52000000-0000-4000-8000-000000000008",
  shaft: "52000000-0000-4000-8000-000000000009",
  generator: "52000000-0000-4000-8000-000000000010",
  pipe: "52000000-0000-4000-8000-000000000011",
  connect: "52000000-0000-4000-8000-000000000012",
  interiorPanel: "52000000-0000-4000-8000-000000000013",
} as const;

// 実マスタの追加10カテゴリ分
// Extra categories mirroring the real master's 10 visible ones
const buildMenuExtraCategorySpecs = [
  { categoryGuid: buildMenuCategoryIds.mining, subCategoryGuid: buildMenuSubCategoryIds.miner, entryId: "53000000-0000-4000-8000-000000000005" },
  { categoryGuid: buildMenuCategoryIds.production, subCategoryGuid: buildMenuSubCategoryIds.primitiveCraft, entryId: "53000000-0000-4000-8000-000000000006" },
  { categoryGuid: buildMenuCategoryIds.mechanicalPower, subCategoryGuid: buildMenuSubCategoryIds.shaft, entryId: "53000000-0000-4000-8000-000000000007" },
  { categoryGuid: buildMenuCategoryIds.electricity, subCategoryGuid: buildMenuSubCategoryIds.generator, entryId: "53000000-0000-4000-8000-000000000008" },
  { categoryGuid: buildMenuCategoryIds.fluid, subCategoryGuid: buildMenuSubCategoryIds.pipe, entryId: "53000000-0000-4000-8000-000000000009" },
  { categoryGuid: buildMenuCategoryIds.tool, subCategoryGuid: buildMenuSubCategoryIds.connect, entryId: "53000000-0000-4000-8000-000000000010" },
  { categoryGuid: buildMenuCategoryIds.buildingMaterial, subCategoryGuid: buildMenuSubCategoryIds.interiorPanel, entryId: "53000000-0000-4000-8000-000000000011" },
] as const;

// スクロール/グリッドQA用量産エントリ
// Filler for grid QA
const buildMenuScrollFillerEntries = Array.from({ length: 80 }, (_, index) => ({
  id: `53000000-0000-4000-8000-0000000010${String(index).padStart(2, "0")}`,
  kind: "block" as const,
  categoryGuid: buildMenuCategoryIds.transport,
  subCategoryGuid: buildMenuSubCategoryIds.car,
  requiredItems: [],
  iconUrl: blockIconUrl(8 + (index % 12)),
}));

// カテゴリ×サブカテゴリ構成。「鉄」検索で 物流/チェスト と 輸送/鉄道 が複数カテゴリ跨ぎでヒットする
// Category x sub-category layout; searching "鉄" hits both 物流/チェスト and 輸送/鉄道 across categories
export const buildMenu = {
  categories: [
    { categoryGuid: buildMenuCategoryIds.logistics, subCategoryGuids: [buildMenuSubCategoryIds.chest, buildMenuSubCategoryIds.conveyor] },
    { categoryGuid: buildMenuCategoryIds.transport, subCategoryGuids: [buildMenuSubCategoryIds.rail, buildMenuSubCategoryIds.car] },
    { categoryGuid: buildMenuCategoryIds.blueprint, subCategoryGuids: [buildMenuSubCategoryIds.saved] },
    // エントリを持たない空カテゴリ。サイドバーの除外分岐を検証するためのもの
    // An empty category with no entries, to exercise the sidebar's exclusion branch
    { categoryGuid: buildMenuCategoryIds.building, subCategoryGuids: [buildMenuSubCategoryIds.foundation] },
    ...buildMenuExtraCategorySpecs.map(({ categoryGuid, subCategoryGuid }) => ({ categoryGuid, subCategoryGuids: [subCategoryGuid] })),
  ],
  // 本番同形のアイコン経路を使う
  // Uses the mock host's production-shaped icon route
  entries: [
    { id: buildMenuEntryIds.woodChest, kind: "block", categoryGuid: "51000000-0000-4000-8000-000000000001", subCategoryGuid: "52000000-0000-4000-8000-000000000001", requiredItems: [{ itemId: 1, count: 4 }], iconUrl: blockIconUrl(1) },
    { id: buildMenuEntryIds.ironChest, kind: "block", categoryGuid: "51000000-0000-4000-8000-000000000001", subCategoryGuid: "52000000-0000-4000-8000-000000000001", requiredItems: [], iconUrl: blockIconUrl(2) },
    // 唯一の複数設置エントリ。財布正規化も検証
    // The sole multi-placement entry; also verifies wallet-key normalization
    { id: buildMenuEntryIds.beltConveyor, kind: "block", categoryGuid: "51000000-0000-4000-8000-000000000001", subCategoryGuid: "52000000-0000-4000-8000-000000000002", requiredItems: [{ itemId: 1, count: 1 }], setPlacement: { perCost: 3, remaining: 2 }, iconUrl: blockIconUrl(3) },
    { id: buildMenuEntryIds.rail, kind: "block", categoryGuid: "51000000-0000-4000-8000-000000000002", subCategoryGuid: "52000000-0000-4000-8000-000000000003", requiredItems: [], iconUrl: blockIconUrl(4) },
    { id: buildMenuEntryIds.cargoCar, kind: "trainCar", categoryGuid: "51000000-0000-4000-8000-000000000002", subCategoryGuid: "52000000-0000-4000-8000-000000000004", requiredItems: [], iconUrl: blockIconUrl(5) },
    { id: buildMenuEntryIds.wireConnectTool, kind: "connectTool", categoryGuid: "51000000-0000-4000-8000-000000000002", subCategoryGuid: "52000000-0000-4000-8000-000000000003", requiredItems: [], iconUrl: blockIconUrl(6) },
    { id: buildMenuEntryIds.blueprintCopy, kind: "blueprintCopy", categoryGuid: "51000000-0000-4000-8000-000000000003", subCategoryGuid: "52000000-0000-4000-8000-000000000005", requiredItems: [] },
    { id: buildMenuEntryIds.starterBaseBlueprint, kind: "blueprint", label: "starter-base", categoryGuid: "51000000-0000-4000-8000-000000000003", subCategoryGuid: "52000000-0000-4000-8000-000000000005", requiredItems: [] },
    ...buildMenuScrollFillerEntries,
    ...buildMenuExtraCategorySpecs.map(({ categoryGuid, subCategoryGuid, entryId }) => ({
      id: entryId,
      kind: "block" as const,
      categoryGuid,
      subCategoryGuid,
      requiredItems: [{ itemId: 1, count: 2 }],
      iconUrl: blockIconUrl(1),
    })),
  ],
} satisfies BuildMenuData;
