import type { BuildMenuData } from "../../../src/bridge/contract/payloadTypes";

// 設置対象idはUnityと同じくGuid文字列。testid・アクション照合でspecが参照するためここを唯一の定義とする
// Placement target ids are GUID strings as in Unity; specs read them for testids and action matching, so this is their single definition
export const buildMenuEntryIds = {
  woodChest: "b10c0000-0000-4000-8000-000000000001",
  ironChest: "b10c0000-0000-4000-8000-000000000002",
  beltConveyor: "b10c0000-0000-4000-8000-000000000003",
  rail: "b10c0000-0000-4000-8000-000000000004",
  cargoCar: "7a1c0000-0000-4000-8000-000000000001",
  starterBaseBlueprint: "3f6a9c1e-8b2d-4f7a-9e3c-1a2b3c4d5e6f",
} as const;

// カテゴリ×サブカテゴリ構成。「鉄」検索で 物流/チェスト と 輸送/鉄道 が複数カテゴリ跨ぎでヒットする
// Category x sub-category layout; searching "鉄" hits both 物流/チェスト and 輸送/鉄道 across categories
export const buildMenu = {
  categories: [
    { name: "物流", subCategories: ["チェスト", "電気コンベア"] },
    { name: "輸送", subCategories: ["鉄道", "車両"] },
    { name: "ブループリント", subCategories: ["保存済み"] },
    // エントリを持たない空カテゴリ。サイドバーの除外分岐を検証するためのもの
    // An empty category with no entries, to exercise the sidebar's exclusion branch
    { name: "建材", subCategories: ["土台"] },
  ],
  entries: [
    { id: buildMenuEntryIds.woodChest, kind: "block", label: "木のチェスト", category: "物流", subCategory: "チェスト", requiredItems: [{ itemId: 1, count: 4 }], iconUrl: "/icons/wood-chest.png" },
    { id: buildMenuEntryIds.ironChest, kind: "block", label: "鉄のチェスト", category: "物流", subCategory: "チェスト", requiredItems: [], iconUrl: "/icons/iron-chest.png" },
    { id: buildMenuEntryIds.beltConveyor, kind: "block", label: "ベルトコンベア", category: "物流", subCategory: "電気コンベア", requiredItems: [], iconUrl: "/icons/belt-conveyor.png" },
    { id: buildMenuEntryIds.rail, kind: "block", label: "鉄道レール", category: "輸送", subCategory: "鉄道", requiredItems: [], iconUrl: "/icons/rail.png" },
    { id: buildMenuEntryIds.cargoCar, kind: "trainCar", label: "貨物車両", category: "輸送", subCategory: "車両", requiredItems: [], iconUrl: "/icons/cargo-car.png" },
    { id: buildMenuEntryIds.starterBaseBlueprint, kind: "blueprint", label: "starter-base", category: "ブループリント", subCategory: "保存済み", requiredItems: [] },
  ],
} satisfies BuildMenuData;
