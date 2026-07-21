// チュートリアルアンカーIDの単一ソース。静的ID一覧と動的IDのprefixをここでのみ定義する
// Single source of truth for tutorial anchor IDs; static IDs and dynamic ID prefixes live only here

export const TutorialAnchorIds = {
  placementHud: "placement.hud",
  deleteHud: "delete.hud",
  gameCrosshair: "game.crosshair",
  trainHudStatus: "train-hud.status",
  pauseMenu: "pause.menu",
  pauseSave: "pause.save",
  pauseBack: "pause.back",
  inventoryCloseButton: "inventory.close-button",
  challengeCurrentHud: "challenge.current-hud",
  recipeCraftButton: "recipe.craft-button",
  miningHud: "mining.hud",
  challengePanel: "challenge.panel",
  challengeCategories: "challenge.categories",
} as const;

export type StaticTutorialAnchorId = (typeof TutorialAnchorIds)[keyof typeof TutorialAnchorIds];

// 動的アンカーIDのprefix。Unity側TutorialAnchorIdMapperの生成規則と対応する
// Dynamic anchor ID prefixes; must mirror the generation rules in Unity's TutorialAnchorIdMapper
export const TutorialAnchorDynamicPrefixes = {
  researchNode: "research.node-",
  recipeItem: "recipe.item-",
  buildMenuEntry: "build-menu.entry-",
  challengeNode: "challenge.node-",
} as const;

export type DynamicTutorialAnchorId =
  `${(typeof TutorialAnchorDynamicPrefixes)[keyof typeof TutorialAnchorDynamicPrefixes]}${string}`;

export function researchNodeAnchorId(guid: string): DynamicTutorialAnchorId {
  return `${TutorialAnchorDynamicPrefixes.researchNode}${guid}`.toLowerCase() as DynamicTutorialAnchorId;
}

export function recipeItemAnchorId(itemId: number): DynamicTutorialAnchorId {
  return `${TutorialAnchorDynamicPrefixes.recipeItem}${itemId}` as DynamicTutorialAnchorId;
}

export function buildMenuEntryAnchorId(entryType: string, entryKey: string): DynamicTutorialAnchorId {
  return `${TutorialAnchorDynamicPrefixes.buildMenuEntry}${entryType}-${entryKey}`.toLowerCase() as DynamicTutorialAnchorId;
}

export function challengeNodeAnchorId(guid: string): DynamicTutorialAnchorId {
  return `${TutorialAnchorDynamicPrefixes.challengeNode}${guid}`.toLowerCase() as DynamicTutorialAnchorId;
}
