import { challengeTutorialTextKey, connectToolNameKey, fluidNameKey, trainCarNameKey } from "../../../src/shared/i18n/contentKeys";

export const WATER_FLUID_GUID = "54000000-0000-4000-8000-000000000001";
export const WIRE_CONNECT_TOOL_GUID = "55000000-0000-4000-8000-000000000001";
export const CARGO_TRAIN_CAR_GUID = "56000000-0000-4000-8000-000000000001";
export const WORLD_PIN_TUTORIAL_GUID = "57000000-0000-4000-8000-000000000001";
export const OUTLINE_LABEL_TUTORIAL_GUID = "58000000-0000-4000-8000-000000000001";
export const KEY_CONTROL_TUTORIAL_GUID = "59000000-0000-4000-8000-000000000001";

const source = {
  [fluidNameKey(WATER_FLUID_GUID)]: "Water",
  "research.11111111-1111-4111-8111-111111111111.name": "最初の研究",
  "research.11111111-1111-4111-8111-111111111111.description": "説明テキスト",
  "research.22222222-2222-4222-8222-222222222222.name": "次の研究",
  "research.22222222-2222-4222-8222-222222222222.description": "前提つき",
  "research.33333333-3333-4333-8333-333333333333.name": "実行可能な研究",
  "research.33333333-3333-4333-8333-333333333333.description": "所持アイテムで研究できる",
  "buildMenuCategory.51000000-0000-4000-8000-000000000001.name": "物流",
  "buildMenuCategory.51000000-0000-4000-8000-000000000002.name": "輸送",
  "buildMenuCategory.51000000-0000-4000-8000-000000000003.name": "ブループリント",
  "buildMenuCategory.51000000-0000-4000-8000-000000000004.name": "建材",
  "buildMenuCategory.51000000-0000-4000-8000-000000000005.name": "採掘",
  "buildMenuCategory.51000000-0000-4000-8000-000000000006.name": "生産",
  "buildMenuCategory.51000000-0000-4000-8000-000000000007.name": "動力",
  "buildMenuCategory.51000000-0000-4000-8000-000000000008.name": "電力",
  "buildMenuCategory.51000000-0000-4000-8000-000000000009.name": "液体",
  "buildMenuCategory.51000000-0000-4000-8000-000000000010.name": "ツール",
  // 実マスタ最長ラベルで1行上限を固定
  // Longest real-master label pins the sidebar's one-line ceiling
  "buildMenuCategory.51000000-0000-4000-8000-000000000011.name": "建築マテリアル",
  "buildMenuSubCategory.52000000-0000-4000-8000-000000000001.name": "チェスト",
  "buildMenuSubCategory.52000000-0000-4000-8000-000000000002.name": "電気コンベア",
  "buildMenuSubCategory.52000000-0000-4000-8000-000000000003.name": "鉄道",
  "buildMenuSubCategory.52000000-0000-4000-8000-000000000004.name": "車両",
  "buildMenuSubCategory.52000000-0000-4000-8000-000000000005.name": "保存済み",
  "buildMenuSubCategory.52000000-0000-4000-8000-000000000006.name": "土台",
  "buildMenuSubCategory.52000000-0000-4000-8000-000000000007.name": "採掘機",
  "buildMenuSubCategory.52000000-0000-4000-8000-000000000008.name": "原始加工",
  "buildMenuSubCategory.52000000-0000-4000-8000-000000000009.name": "シャフト",
  "buildMenuSubCategory.52000000-0000-4000-8000-000000000010.name": "発電",
  "buildMenuSubCategory.52000000-0000-4000-8000-000000000011.name": "パイプ",
  "buildMenuSubCategory.52000000-0000-4000-8000-000000000012.name": "接続",
  "buildMenuSubCategory.52000000-0000-4000-8000-000000000013.name": "内装パネル",
  "block.53000000-0000-4000-8000-000000000001.name": "木のチェスト",
  "block.53000000-0000-4000-8000-000000000002.name": "鉄のチェスト",
  "block.53000000-0000-4000-8000-000000000003.name": "ベルトコンベア",
  "block.53000000-0000-4000-8000-000000000004.name": "鉄道レール",
  // 「鉄」検索ヒット数維持のため含めない
  // Excludes iron-named entries to keep search-hit count stable
  "block.53000000-0000-4000-8000-000000000005.name": "電動採掘機",
  "block.53000000-0000-4000-8000-000000000006.name": "石の加工台",
  "block.53000000-0000-4000-8000-000000000007.name": "動力シャフト",
  "block.53000000-0000-4000-8000-000000000008.name": "石炭発電機",
  "block.53000000-0000-4000-8000-000000000009.name": "銅パイプ",
  "block.53000000-0000-4000-8000-000000000010.name": "電線接続具",
  "block.53000000-0000-4000-8000-000000000011.name": "装飾パネル",
  [connectToolNameKey(WIRE_CONNECT_TOOL_GUID)]: "電線接続ツール",
  [trainCarNameKey(CARGO_TRAIN_CAR_GUID)]: "貨物車両",
  [challengeTutorialTextKey(WORLD_PIN_TUTORIAL_GUID)]: "小石を拾う",
  [challengeTutorialTextKey(OUTLINE_LABEL_TUTORIAL_GUID)]: "照準に合わせる",
  [challengeTutorialTextKey(KEY_CONTROL_TUTORIAL_GUID)]: "Tabでインベントリを開く",
  "ui.buildMenu.blueprintCopy": "ブループリントコピー",
  "challengeCategory.81000000-0000-4000-8000-000000000001.name": "Basics",
  "challenge.82000000-0000-4000-8000-000000000001.title": "First Craft",
  "challenge.82000000-0000-4000-8000-000000000001.summary": "craft something",
  "challenge.82000000-0000-4000-8000-000000000002.title": "Second Step",
  "challenge.82000000-0000-4000-8000-000000000002.summary": "keep going",
  "challenge.82000000-0000-4000-8000-000000000003.title": "石を採掘する",
  "challenge.82000000-0000-4000-8000-000000000004.title": "石を採掘する",
  "challenge.82000000-0000-4000-8000-000000000005.title": "石器をクラフトする",
  "challenge.82000000-0000-4000-8000-000000000006.title": "木を伐採して拠点へ運ぶ",
  "challenge.82000000-0000-4000-8000-000000000007.title": "VeryLongUnbrokenChallengeObjectiveTextThatMustWrapInsideTheHudWithoutOverflowingAndStillRemainReadableAcrossEveryMenuScreenWithoutChangingTheChallengeHudLayout",
  "challenge.82000000-0000-4000-8000-000000000008.title": "地下深くにある非常に長い名前の鉱床を見つけて必要な石を採掘する",
  "challenge.82000000-0000-4000-8000-000000000009.title": "遠方の森林から建築に必要な木材を伐採して拠点まで運搬する",
  "challenge.82000000-0000-4000-8000-00000000000a.title": "VeryLongUnbrokenSecondaryObjectiveTextThatMustAlsoWrapInsideTheHud",
};

// 実マスタ英訳をそのまま転記
// Transcribed verbatim from the real v8 master's localization.csv
const buildMenuEnglish = {
  "buildMenuCategory.51000000-0000-4000-8000-000000000001.name": "Logistics",
  "buildMenuCategory.51000000-0000-4000-8000-000000000002.name": "Transport",
  "buildMenuCategory.51000000-0000-4000-8000-000000000003.name": "Blueprint",
  "buildMenuCategory.51000000-0000-4000-8000-000000000004.name": "Building Materials",
  "buildMenuCategory.51000000-0000-4000-8000-000000000005.name": "Mining",
  "buildMenuCategory.51000000-0000-4000-8000-000000000006.name": "Production",
  "buildMenuCategory.51000000-0000-4000-8000-000000000007.name": "Power",
  "buildMenuCategory.51000000-0000-4000-8000-000000000008.name": "Electricity",
  "buildMenuCategory.51000000-0000-4000-8000-000000000009.name": "Liquids",
  "buildMenuCategory.51000000-0000-4000-8000-000000000010.name": "Tools",
  // 建材と同じ実マスタ最長英訳を再利用
  // Reuses longest name
  "buildMenuCategory.51000000-0000-4000-8000-000000000011.name": "Building Materials",
  "buildMenuSubCategory.52000000-0000-4000-8000-000000000001.name": "Chests",
  "buildMenuSubCategory.52000000-0000-4000-8000-000000000002.name": "Electric Conveyors",
  "buildMenuSubCategory.52000000-0000-4000-8000-000000000003.name": "Railways",
  "buildMenuSubCategory.52000000-0000-4000-8000-000000000004.name": "Vehicles",
  "buildMenuSubCategory.52000000-0000-4000-8000-000000000005.name": "Saved",
  "buildMenuSubCategory.52000000-0000-4000-8000-000000000006.name": "Foundations",
  "buildMenuSubCategory.52000000-0000-4000-8000-000000000007.name": "Miners",
  "buildMenuSubCategory.52000000-0000-4000-8000-000000000008.name": "Primitive Processing",
  "buildMenuSubCategory.52000000-0000-4000-8000-000000000009.name": "Shafts",
  "buildMenuSubCategory.52000000-0000-4000-8000-000000000010.name": "Power Generation",
  "buildMenuSubCategory.52000000-0000-4000-8000-000000000011.name": "Pipes",
  "buildMenuSubCategory.52000000-0000-4000-8000-000000000012.name": "Connections",
  "buildMenuSubCategory.52000000-0000-4000-8000-000000000013.name": "Interior Panels",
  "block.53000000-0000-4000-8000-000000000001.name": "Wooden Chest",
  "block.53000000-0000-4000-8000-000000000002.name": "Iron Chest",
  "block.53000000-0000-4000-8000-000000000003.name": "Belt Conveyor",
  "block.53000000-0000-4000-8000-000000000004.name": "Railway Rail",
  "block.53000000-0000-4000-8000-000000000005.name": "Electric Miner",
  "block.53000000-0000-4000-8000-000000000006.name": "Stone Workbench",
  "block.53000000-0000-4000-8000-000000000007.name": "Power Shaft",
  "block.53000000-0000-4000-8000-000000000008.name": "Coal Generator",
  "block.53000000-0000-4000-8000-000000000009.name": "Copper Pipe",
  "block.53000000-0000-4000-8000-000000000010.name": "Wire Connector",
  "block.53000000-0000-4000-8000-000000000011.name": "Decorative Panel",
  [connectToolNameKey(WIRE_CONNECT_TOOL_GUID)]: "Wire Connect Tool",
  [trainCarNameKey(CARGO_TRAIN_CAR_GUID)]: "Cargo Car",
  "ui.buildMenu.blueprintCopy": "Blueprint Copy",
};

export const contentLocalizationDictionaries: Record<string, Record<string, string>> = {
  source,
  english: {
    ...source,
    ...buildMenuEnglish,
    "challenge.82000000-0000-4000-8000-000000000003.title": "Mine stone",
  },
  japanese: source,
};
