// messageId→表示テンプレートの対応表。文言はWeb側が所有しサーバーは構造化IDのみ送る
// Maps messageId to display templates; the web owns wording, the server sends structured ids only
import { L, buildPositionalInterpolationValues, challengeTitleKey, researchNameKey, type TranslationKey } from "@/shared/i18n";
import type { GameNotification } from "./notificationStore";

const notificationKeys = new Map<string, TranslationKey>([
  ["achievement.researchCompleted", L.ui.notification.researchCompleted],
  ["achievement.challengeCompleted", L.ui.notification.challengeCompleted],
  ["achievement.unlockedItem", L.ui.notification.unlockedItem],
  ["achievement.unlockedCraftRecipe", L.ui.notification.unlockedCraftRecipe],
  ["achievement.unlockedMachineRecipe", L.ui.notification.unlockedMachineRecipe],
  ["achievement.unlockedBlock", L.ui.notification.unlockedBlock],
  ["achievement.unlockedTrainCar", L.ui.notification.unlockedTrainCar],
  ["achievement.unlockedConnectTool", L.ui.notification.unlockedConnectTool],
  ["achievement.unlockedBlueprint", L.ui.notification.unlockedBlueprint],
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
  ["denied.electricWireExtend.OutOfRange", L.ui.notification.electricWireExtendOutOfRange],
  ["denied.electricWireExtend.AlreadyConnected", L.ui.notification.electricWireExtendAlreadyConnected],
  ["denied.electricWireExtend.ConnectionLimit", L.ui.notification.electricWireExtendConnectionLimit],
  ["denied.electricWireExtend.NoWireItem", L.ui.notification.electricWireExtendNoWireItem],
  ["denied.electricWireExtend.NoPoleItem", L.ui.notification.electricWireExtendNoPoleItem],
  ["denied.electricWireExtend.InvalidTarget", L.ui.notification.electricWireExtendInvalidTarget],
  ["denied.electricWireExtend.PositionOccupied", L.ui.notification.electricWireExtendPositionOccupied],
  ["denied.electricWireExtend.NotUnlocked", L.ui.notification.electricWireExtendNotUnlocked],
  ["denied.electricWireExtend.InsufficientItems", L.ui.notification.electricWireExtendInsufficientItems],
  ["denied.electricWireExtend.InvalidMode", L.ui.notification.electricWireExtendFailed],
  ["denied.electricWireExtend.None", L.ui.notification.electricWireExtendFailed],
  ["denied.electricWireDisconnect.NotConnected", L.ui.notification.electricWireDisconnectNotConnected],
  ["denied.electricWireDisconnect.InventoryFull", L.ui.notification.electricWireDisconnectInventoryFull],
  ["denied.electricWireDisconnect.InvalidTarget", L.ui.notification.electricWireDisconnectFailed],
  ["denied.blueprint.NotUnlocked", L.ui.notification.blueprintNotUnlocked],
]);

// 外部IDを有限の型付きキーへ閉じ、未知IDも専用キーで可視化する
// Close external ids into finite typed keys and surface unknown ids through a dedicated key
// 獲得通知はcategory側でキーを決めるため表には載せない
// Earned rows get their key from the category, so they are absent from this table
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

export function buildInterpolationValues(messageId: string, messageParams: string[]) {
  return {
    messageId,
    ...buildPositionalInterpolationValues(messageParams),
  };
}

// 表示キーと補間値を同じnarrowから作り、アイテム名とcountを持つのは獲得通知だけという不変条件を1箇所に閉じる
// The key and the interpolation values come from one narrow, closing the "only earned rows carry an item name and a count" invariant in a single place
export function resolveNotificationText(
  notification: GameNotification,
  translate: (key: TranslationKey) => string,
  resolveItemDisplayName: (itemId: number) => string,
) {
  const values = buildInterpolationValues(
    notification.messageId,
    resolveNotificationParams(notification.messageId, notification.messageParams, translate),
  );

  switch (notification.category) {
    case "itemEarned":
      // キーはcategoryで確定させ、messageIdのドリフトで生トークンが出るのを防ぐ
      // The category fixes the key so a drifting messageId cannot leak raw tokens
      return {
        key: L.ui.notification.itemEarned,
        values: { ...values, itemName: resolveItemDisplayName(notification.itemId), count: notification.count },
      };
    case "achievement":
    case "operationDenied":
      return { key: resolveNotificationKey(notification.messageId), values };
    default: {
      // categoryが増えたらここがコンパイルエラーになる
      // Adding a category turns this into a compile error
      const unreachable: never = notification;
      return unreachable;
    }
  }
}
