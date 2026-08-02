import { fluidNameKey } from "../../../src/shared/i18n/contentKeys";

export const WATER_FLUID_GUID = "54000000-0000-4000-8000-000000000001";

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
  "buildMenuSubCategory.52000000-0000-4000-8000-000000000001.name": "チェスト",
  "buildMenuSubCategory.52000000-0000-4000-8000-000000000002.name": "電気コンベア",
  "buildMenuSubCategory.52000000-0000-4000-8000-000000000003.name": "鉄道",
  "buildMenuSubCategory.52000000-0000-4000-8000-000000000004.name": "車両",
  "buildMenuSubCategory.52000000-0000-4000-8000-000000000005.name": "保存済み",
  "buildMenuSubCategory.52000000-0000-4000-8000-000000000006.name": "土台",
  "block.53000000-0000-4000-8000-000000000001.name": "木のチェスト",
  "block.53000000-0000-4000-8000-000000000002.name": "鉄のチェスト",
  "block.53000000-0000-4000-8000-000000000003.name": "ベルトコンベア",
  "block.53000000-0000-4000-8000-000000000004.name": "鉄道レール",
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

export const contentLocalizationDictionaries: Record<string, Record<string, string>> = {
  source,
  english: {
    ...source,
    "ui.buildMenu.blueprintCopy": "Blueprint Copy",
    "challenge.82000000-0000-4000-8000-000000000003.title": "Mine stone",
  },
  japanese: source,
};
