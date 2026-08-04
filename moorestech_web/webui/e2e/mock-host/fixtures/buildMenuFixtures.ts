import type { BuildMenuData } from "../../../src/bridge/contract/payloadTypes";
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
} as const;

export const buildMenuSubCategoryIds = {
  chest: "52000000-0000-4000-8000-000000000001",
  conveyor: "52000000-0000-4000-8000-000000000002",
  rail: "52000000-0000-4000-8000-000000000003",
  car: "52000000-0000-4000-8000-000000000004",
  saved: "52000000-0000-4000-8000-000000000005",
  foundation: "52000000-0000-4000-8000-000000000006",
} as const;

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
  ],
  entries: [
    { id: buildMenuEntryIds.woodChest, kind: "block", categoryGuid: "51000000-0000-4000-8000-000000000001", subCategoryGuid: "52000000-0000-4000-8000-000000000001", requiredItems: [{ itemId: 1, count: 4 }], iconUrl: "/icons/wood-chest.png" },
    { id: buildMenuEntryIds.ironChest, kind: "block", categoryGuid: "51000000-0000-4000-8000-000000000001", subCategoryGuid: "52000000-0000-4000-8000-000000000001", requiredItems: [], iconUrl: "/icons/iron-chest.png" },
    { id: buildMenuEntryIds.beltConveyor, kind: "block", categoryGuid: "51000000-0000-4000-8000-000000000001", subCategoryGuid: "52000000-0000-4000-8000-000000000002", requiredItems: [], iconUrl: "/icons/belt-conveyor.png" },
    { id: buildMenuEntryIds.rail, kind: "block", categoryGuid: "51000000-0000-4000-8000-000000000002", subCategoryGuid: "52000000-0000-4000-8000-000000000003", requiredItems: [], iconUrl: "/icons/rail.png" },
    { id: buildMenuEntryIds.cargoCar, kind: "trainCar", categoryGuid: "51000000-0000-4000-8000-000000000002", subCategoryGuid: "52000000-0000-4000-8000-000000000004", requiredItems: [], iconUrl: "/icons/cargo-car.png" },
    { id: buildMenuEntryIds.wireConnectTool, kind: "connectTool", categoryGuid: "51000000-0000-4000-8000-000000000002", subCategoryGuid: "52000000-0000-4000-8000-000000000003", requiredItems: [], iconUrl: "/icons/wire-tool.png" },
    { id: buildMenuEntryIds.blueprintCopy, kind: "blueprintCopy", categoryGuid: "51000000-0000-4000-8000-000000000003", subCategoryGuid: "52000000-0000-4000-8000-000000000005", requiredItems: [] },
    { id: buildMenuEntryIds.starterBaseBlueprint, kind: "blueprint", label: "starter-base", categoryGuid: "51000000-0000-4000-8000-000000000003", subCategoryGuid: "52000000-0000-4000-8000-000000000005", requiredItems: [] },
  ],
} satisfies BuildMenuData;
