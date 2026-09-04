import { blockNameKey } from "../../../src/shared/i18n/contentKeys";

export const CHEST_BLOCK_GUID = "00000000-0000-4000-8000-000000000201";
export const TANK_BLOCK_GUID = "00000000-0000-4000-8000-000000000202";
export const ELECTRIC_MACHINE_BLOCK_GUID = "11111111-1111-4111-8111-111111111111";
export const GEAR_MACHINE_BLOCK_GUID = "22222222-2222-4222-8222-222222222222";
export const GENERATOR_BLOCK_GUID = "00000000-0000-4000-8000-000000000205";
export const MINER_BLOCK_GUID = "00000000-0000-4000-8000-000000000206";
export const GEAR_MINER_BLOCK_GUID = "00000000-0000-4000-8000-000000000207";
export const GENERIC_BLOCK_GUID = "00000000-0000-4000-8000-000000000208";
export const FILTER_SPLITTER_BLOCK_GUID = "00000000-0000-4000-8000-000000000209";
export const ELECTRIC_TO_GEAR_BLOCK_GUID = "00000000-0000-4000-8000-000000000210";
export const TRAIN_ITEM_PLATFORM_BLOCK_GUID = "00000000-0000-4000-8000-000000000211";
export const TRAIN_FLUID_PLATFORM_BLOCK_GUID = "00000000-0000-4000-8000-000000000212";
export const ELECTRIC_POLE_BLOCK_GUID = "00000000-0000-4000-8000-000000000213";

// 動的ブロック名はバニラCSV外なのでmockの合成辞書へ足す
// Dynamic block names live outside the vanilla CSV, so add them to the mock composite dictionary
const names = [
  [CHEST_BLOCK_GUID, "Chest", "Chest"],
  [TANK_BLOCK_GUID, "Fluid Tank", "Fluid Tank"],
  [ELECTRIC_MACHINE_BLOCK_GUID, "Electric Machine", "電気機械"],
  [GEAR_MACHINE_BLOCK_GUID, "Gear Machine", "ギア機械"],
  [GENERATOR_BLOCK_GUID, "Generator", "発電機"],
  [MINER_BLOCK_GUID, "Electric Miner", "電動採掘機"],
  [GEAR_MINER_BLOCK_GUID, "Gear Miner", "ギア採掘機"],
  [GENERIC_BLOCK_GUID, "Generic Block", "汎用ブロック"],
  [FILTER_SPLITTER_BLOCK_GUID, "Filter Splitter", "フィルタ分岐器"],
  [ELECTRIC_TO_GEAR_BLOCK_GUID, "Rotation Generator", "回転生成機"],
  [TRAIN_ITEM_PLATFORM_BLOCK_GUID, "Cargo Platform", "貨物プラットフォーム"],
  [TRAIN_FLUID_PLATFORM_BLOCK_GUID, "Fluid Platform", "液体プラットフォーム"],
  [ELECTRIC_POLE_BLOCK_GUID, "Electric Pole", "電柱"],
] as const;

const english = Object.fromEntries(names.map(([guid, name]) => [blockNameKey(guid), name]));
const japanese = Object.fromEntries(names.map(([guid, , name]) => [blockNameKey(guid), name]));

// germanは本番と同じくenglish複写（.decisions/2026-08-25-german.jsonはenglish複写でlocaleとnameだけ独語にする）
// german mirrors production by copying english (.decisions/2026-08-25)
export const blockNameDictionaries: Record<string, Record<string, string>> = {
  source: japanese,
  english,
  japanese,
  german: english,
};
