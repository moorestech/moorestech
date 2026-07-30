const source = {
  "research.11111111-1111-1111-1111-111111111111.name": "最初の研究",
  "research.11111111-1111-1111-1111-111111111111.description": "説明テキスト",
  "research.22222222-2222-2222-2222-222222222222.name": "次の研究",
  "research.22222222-2222-2222-2222-222222222222.description": "前提つき",
  "research.33333333-3333-3333-3333-333333333333.name": "実行可能な研究",
  "research.33333333-3333-3333-3333-333333333333.description": "所持アイテムで研究できる",
  "challengeCategory.cat-1.name": "Basics",
  "challenge.ch-1.title": "First Craft",
  "challenge.ch-1.summary": "craft something",
  "challenge.ch-2.title": "Second Step",
  "challenge.ch-2.summary": "keep going",
  "challenge.ch-jp.title": "石を採掘する",
  "challenge.ch-a.title": "石を採掘する",
  "challenge.ch-b.title": "石器をクラフトする",
  "challenge.ch-c.title": "木を伐採して拠点へ運ぶ",
  "challenge.ch-long.title": "VeryLongUnbrokenChallengeObjectiveTextThatMustWrapInsideTheHudWithoutOverflowing",
  "challenge.ch-ml-a.title": "地下深くにある非常に長い名前の鉱床を見つけて必要な石を採掘する",
  "challenge.ch-ml-b.title": "遠方の森林から建築に必要な木材を伐採して拠点まで運搬する",
  "challenge.ch-ml-c.title": "VeryLongUnbrokenSecondaryObjectiveTextThatMustAlsoWrapInsideTheHud",
};

export const contentLocalizationDictionaries: Record<string, Record<string, string>> = {
  source,
  english: {
    ...source,
    "challenge.ch-jp.title": "Mine stone",
  },
  japanese: source,
};
