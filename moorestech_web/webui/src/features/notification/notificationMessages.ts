// messageId→表示テンプレートの対応表。文言はWeb側が所有しサーバーは構造化IDのみ送る
// Maps messageId to display templates; the web owns wording, the server sends structured ids only
import { L, challengeTitleKey, researchNameKey, type TranslationKey } from "@/shared/i18n";

const notificationKeys = new Map<string, TranslationKey>([
  ["achievement.researchCompleted", L.ui.notification.researchCompleted],
  ["achievement.challengeCompleted", L.ui.notification.challengeCompleted],
  ["achievement.unlockedItem", L.ui.notification.unlockedItem],
  ["achievement.unlockedCraftRecipe", L.ui.notification.unlockedCraftRecipe],
  ["achievement.unlockedMachineRecipe", L.ui.notification.unlockedMachineRecipe],
  ["achievement.unlockedBlock", L.ui.notification.unlockedBlock],
  ["achievement.unlockedTrainCar", L.ui.notification.unlockedTrainCar],
  ["achievement.unlockedConnectTool", L.ui.notification.unlockedConnectTool],
  ["denied.researchNotCompletable", L.ui.notification.researchNotCompletable],
  ["denied.craftResultFull", L.ui.notification.craftResultFull],
  ["denied.craftMaterialShortage", L.ui.notification.craftMaterialShortage],
  ["denied.removeTrainCarInventoryFull", L.ui.notification.removeTrainCarInventoryFull],
  ["denied.placeBlockNotUnlocked", L.ui.notification.placeBlockNotUnlocked],
  ["denied.placeBlockCostShortage", L.ui.notification.placeBlockCostShortage],
  ["denied.placeBlockWireShortage", L.ui.notification.placeBlockWireShortage],
  ["denied.railEdit.InvalidNode", L.ui.notification.railEditInvalidNode],
  ["denied.railEdit.NodeInUseByTrain", L.ui.notification.railEditNodeInUseByTrain],
  ["denied.railEdit.StationInternalEdge", L.ui.notification.railEditStationInternalEdge],
  ["denied.railEdit.InvalidMode", L.ui.notification.railEditInvalidMode],
  ["denied.railEdit.NotEnoughRailItem", L.ui.notification.railEditNotEnoughRailItem],
  ["denied.railEdit.NotEnoughInventorySpace", L.ui.notification.railEditNotEnoughInventorySpace],
  ["denied.railEdit.RailLengthExceeded", L.ui.notification.railEditRailLengthExceeded],
  ["denied.railEdit.NotUnlocked", L.ui.notification.railEditNotUnlocked],
  ["denied.railEdit.UnknownError", L.ui.notification.railEditUnknownError],
]);

// 外部IDを有限の型付きキーへ閉じ、未知IDも専用キーで可視化する
// Close external ids into finite typed keys and surface unknown ids through a dedicated key
export function resolveNotificationKey(messageId: string): TranslationKey {
  return notificationKeys.get(messageId) ?? L.ui.notification.unknownMessage;
}

// Guidパラメータを持つ通知のcontentキー組み立て表。サーバーは表示名でなくGuidを送る
// Content-key builders for GUID-bearing notifications; the server sends GUIDs, not display names
const contentParamKeyBuilders = new Map<string, (guid: string) => TranslationKey>([
  ["achievement.researchCompleted", researchNameKey],
  ["achievement.challengeCompleted", challengeTitleKey],
]);

export function resolveNotificationParams(
  messageId: string,
  messageParams: string[],
  translate: (key: TranslationKey) => string,
): string[] {
  const buildContentKey = contentParamKeyBuilders.get(messageId);
  if (!buildContentKey) return messageParams;
  return messageParams.map((guid) => translate(buildContentKey(guid)));
}

export function buildInterpolationValues(messageId: string, messageParams: string[]): Record<string, string> {
  return {
    messageId,
    ...Object.fromEntries(messageParams.map((value, index) => [`p${index}`, value])),
  };
}
