// messageId→表示テンプレートの対応表。文言はWeb側が所有しサーバーは構造化IDのみ送る
// Maps messageId to display templates; the web owns wording, the server sends structured ids only
// keyはi18n辞書キーも兼ねる（{p0}等はparamsで補間）
// The key also doubles as the i18n dictionary key; {p0} etc. are filled from params
const templates: Record<string, string> = {
  "achievement.researchCompleted": "Research completed: {p0}",
  "achievement.challengeCompleted": "Challenge completed: {p0}",
  "achievement.unlockedItem": "New item unlocked",
  "achievement.unlockedCraftRecipe": "New crafting recipe unlocked",
  "achievement.unlockedMachineRecipe": "New machine recipe unlocked",
  "achievement.unlockedBlock": "New block unlocked",
  "achievement.unlockedTrainCar": "New train car unlocked",
  "achievement.unlockedConnectTool": "New connect tool unlocked",
  "denied.researchNotCompletable": "Cannot complete research (prerequisites or materials missing)",
  "denied.craftResultFull": "Cannot craft: inventory is full",
  "denied.craftMaterialShortage": "Cannot craft: not enough materials",
  "denied.removeTrainCarInventoryFull": "Cannot remove train car: inventory is full",
  "denied.placeBlockNotUnlocked": "Some blocks were not placed: not unlocked yet",
  "denied.placeBlockCostShortage": "Some blocks were not placed: not enough materials",
  "denied.placeBlockWireShortage": "Some blocks were not placed: not enough wires",
  "denied.railEdit.InvalidNode": "Rail edit failed: invalid rail node",
  "denied.railEdit.NodeInUseByTrain": "Rail edit failed: a train is using this rail",
  "denied.railEdit.StationInternalEdge": "Rail edit failed: cannot edit station internal rail",
  "denied.railEdit.InvalidMode": "Rail edit failed",
  "denied.railEdit.NotEnoughRailItem": "Rail edit failed: not enough rail materials",
  "denied.railEdit.NotEnoughInventorySpace": "Rail edit failed: inventory is full",
  "denied.railEdit.RailLengthExceeded": "Rail edit failed: rail is too long",
  "denied.railEdit.NotUnlocked": "Rail edit failed: connect tool not unlocked",
  "denied.railEdit.UnknownError": "Rail edit failed",
};

// 未知IDはそのまま表示し欠落可視化
// Unknown ids are shown as-is to surface gaps
export function resolveNotificationTemplate(messageId: string): string {
  return templates[messageId] ?? messageId;
}

export function buildInterpolationValues(messageParams: string[]): Record<string, string> {
  return Object.fromEntries(messageParams.map((value, index) => [`p${index}`, value]));
}
