// messageId→表示テンプレートの対応表。文言はWeb側が所有しサーバーは構造化IDのみ送る
// Maps messageId to display templates; the web owns wording, the server sends structured ids only
import { L, challengeTitleKey, researchNameKey, type TranslationKey } from "@/shared/i18n";
import type { GameNotification } from "./notificationStore";

const notificationKeys = new Map<string, TranslationKey>([
  ["denied.undoRestoreSkipped", L.ui.notification.undoRestoreSkipped],
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
  ["denied.deleteUnconfirmed", L.ui.notification.deleteUnconfirmed],
  ["denied.deleteRejected", L.ui.notification.deleteRejected],
  ["denied.removeTrainCarInventoryFull", L.ui.notification.removeTrainCarInventoryFull],
  ["denied.inventoryMoveSlotRejected", L.ui.notification.inventoryMoveSlotRejected],
  ["denied.inventoryMovePartialSwap", L.ui.notification.inventoryMovePartialSwap],
  ["denied.miningInventoryFull", L.ui.notification.miningInventoryFull],
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
  ["denied.gearChainDisconnect.NotConnected", L.ui.notification.gearChainDisconnectNotConnected],
  ["denied.gearChainDisconnect.InventoryFull", L.ui.notification.gearChainDisconnectInventoryFull],
  ["denied.gearChainDisconnect.InvalidTarget", L.ui.notification.gearChainDisconnectFailed],
  ["denied.gearChainConnect.InvalidTarget", L.ui.notification.gearChainConnectInvalidTarget],
  ["denied.gearChainConnect.NotUnlocked", L.ui.notification.gearChainConnectNotUnlocked],
  ["denied.gearChainConnect.TooFar", L.ui.notification.gearChainConnectTooFar],
  ["denied.gearChainConnect.AlreadyConnected", L.ui.notification.gearChainConnectAlreadyConnected],
  ["denied.gearChainConnect.ConnectionLimit", L.ui.notification.gearChainConnectConnectionLimit],
  ["denied.gearChainConnect.NoItem", L.ui.notification.gearChainConnectNoItem],
  ["denied.blueprint.NotUnlocked", L.ui.notification.blueprintNotUnlocked],
  ["denied.blueprint.PasteCostShortage", L.ui.notification.blueprintPasteCostShortage],
  ["denied.blueprint.PasteNotUnlocked", L.ui.notification.blueprintPasteNotUnlocked],
  ["denied.blueprint.PasteLineFailed", L.ui.notification.blueprintPasteLineFailed],
  ["denied.blueprint.NotFound", L.ui.notification.blueprintNotFound],
  ["denied.blueprint.InvalidRequest", L.ui.notification.blueprintPasteInvalidRequest],
  ["denied.blueprint.PastePlacementFailed", L.ui.notification.blueprintPastePlacementFailed],
  ["denied.blueprintCreate.RequestFailed", L.ui.notification.blueprintCreateRequestFailed],
  ["denied.blueprintCreate.InvalidName", L.ui.notification.blueprintCreateInvalidName],
  ["denied.blueprintCreate.EmptyArea", L.ui.notification.blueprintCreateEmptyArea],
  ["denied.blueprintCreate.InvalidRequest", L.ui.notification.blueprintCreateInvalidRequest],
  ["denied.blueprintCreate.Unknown", L.ui.notification.blueprintCreateUnknown],
  ["saveMigration.missingMasterPruned", L.ui.notification.saveMigrationMissingMasterPruned],
]);

// 外部IDを有限の型付きキーへ閉じ、未知IDも専用キーで可視化する
// Close external ids into finite typed keys and surface unknown ids through a dedicated key
// 獲得通知はcategory側でキーを決めるため表には載せない
// Earned rows get their key from the category, so they are absent from this table
export function resolveNotificationKey(messageId: string): TranslationKey {
  return notificationKeys.get(messageId) ?? L.ui.notification.unknownMessage;
}

// Guidを運ぶ通知の、値の名前とcontentキー組み立て。サーバーは表示名でなくGuidを送る
// Value name and content-key builder for GUID-bearing notifications; the server sends GUIDs, not display names
const contentParamKeyBuilders = new Map<string, { name: string; buildKey: (guid: string) => TranslationKey }>([
  ["achievement.researchCompleted", { name: "research", buildKey: researchNameKey }],
  ["achievement.challengeCompleted", { name: "challenge", buildKey: challengeTitleKey }],
]);

export function resolveNotificationParams(
  messageId: string,
  messageParams: Record<string, string>,
  translate: (key: TranslationKey) => string,
): Record<string, string> {
  const builder = contentParamKeyBuilders.get(messageId);
  if (!builder) return messageParams;
  // 名前が欠けた通知は生の枠のまま出るので、開発時に気づけるよう警告する
  // A notification missing the named value shows a raw token, so warn so developers notice
  if (!Object.hasOwn(messageParams, builder.name)) {
    console.warn(`[notification] ${messageId}: missing param ${builder.name}`);
    return messageParams;
  }
  return { ...messageParams, [builder.name]: translate(builder.buildKey(messageParams[builder.name])) };
}

export function buildInterpolationValues(messageId: string, messageParams: Record<string, string>) {
  return { ...messageParams, messageId };
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
    case "saveMigration":
      return { key: resolveNotificationKey(notification.messageId), values };
    default: {
      // categoryが増えたらここがコンパイルエラーになる
      // Adding a category turns this into a compile error
      const unreachable: never = notification;
      return unreachable;
    }
  }
}
