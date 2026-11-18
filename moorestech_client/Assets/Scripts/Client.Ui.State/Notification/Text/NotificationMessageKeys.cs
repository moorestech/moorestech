using System.Collections.Generic;
using Client.Localization;
using Client.Ui.State.Notification.Params;
using Mooresmaster.Localization.Generated;

namespace Client.Ui.State.Notification.Text
{
    /// <summary>
    /// サーバーの messageId を有限の辞書キーへ閉じる表。未知の id も専用の文言で見せる。獲得はカテゴリでキーが決まるので載せない（Web notificationKeys）
    /// Closes server messageIds into finite dictionary keys and shows unknown ids through a dedicated text; earns take their key from the category and are absent (Web notificationKeys)
    /// </summary>
    public static class NotificationMessageKeys
    {
        private static readonly Dictionary<string, LocalizationKey> Keys = new()
        {
            ["denied.undoRestoreSkipped"] = LocalizationKeys.Ui.Notification.UndoRestoreSkipped,
            [NotificationParams.ResearchCompletedId] = LocalizationKeys.Ui.Notification.ResearchCompleted,
            [NotificationParams.ChallengeCompletedId] = LocalizationKeys.Ui.Notification.ChallengeCompleted,
            ["achievement.unlockedItem"] = LocalizationKeys.Ui.Notification.UnlockedItem,
            ["achievement.unlockedCraftRecipe"] = LocalizationKeys.Ui.Notification.UnlockedCraftRecipe,
            ["achievement.unlockedMachineRecipe"] = LocalizationKeys.Ui.Notification.UnlockedMachineRecipe,
            ["achievement.unlockedBlock"] = LocalizationKeys.Ui.Notification.UnlockedBlock,
            ["achievement.unlockedTrainCar"] = LocalizationKeys.Ui.Notification.UnlockedTrainCar,
            ["achievement.unlockedConnectTool"] = LocalizationKeys.Ui.Notification.UnlockedConnectTool,
            ["achievement.unlockedBlueprint"] = LocalizationKeys.Ui.Notification.UnlockedBlueprint,
            ["denied.researchNotCompletable"] = LocalizationKeys.Ui.Notification.ResearchNotCompletable,
            ["denied.craftResultFull"] = LocalizationKeys.Ui.Notification.CraftResultFull,
            ["denied.craftMaterialShortage"] = LocalizationKeys.Ui.Notification.CraftMaterialShortage,
            ["denied.deleteUnconfirmed"] = LocalizationKeys.Ui.Notification.DeleteUnconfirmed,
            ["denied.deleteRejected"] = LocalizationKeys.Ui.Notification.DeleteRejected,
            ["denied.removeTrainCarInventoryFull"] = LocalizationKeys.Ui.Notification.RemoveTrainCarInventoryFull,
            ["denied.inventoryMoveUnresolved"] = LocalizationKeys.Ui.BlockInventory.InventoryOpenFailed,
            ["denied.inventoryOpenFailed"] = LocalizationKeys.Ui.BlockInventory.InventoryOpenFailed,
            ["denied.inventoryDetailLoading"] = LocalizationKeys.Ui.BlockInventory.DetailLoading,
            ["denied.inventoryMoveSlotRejected"] = LocalizationKeys.Ui.Notification.InventoryMoveSlotRejected,
            ["denied.inventoryMovePartialSwap"] = LocalizationKeys.Ui.Notification.InventoryMovePartialSwap,
            ["denied.miningInventoryFull"] = LocalizationKeys.Ui.Notification.MiningInventoryFull,
            ["denied.placeBlockNotUnlocked"] = LocalizationKeys.Ui.Notification.PlaceBlockNotUnlocked,
            ["denied.placeBlockCostShortage"] = LocalizationKeys.Ui.Notification.PlaceBlockCostShortage,
            ["denied.placeBlockWireShortage"] = LocalizationKeys.Ui.Notification.PlaceBlockWireShortage,
            ["denied.railEdit.InvalidNode"] = LocalizationKeys.Ui.Notification.RailEditInvalidNode,
            ["denied.railEdit.NodeInUseByTrain"] = LocalizationKeys.Ui.Notification.RailEditNodeInUseByTrain,
            ["denied.railEdit.StationInternalEdge"] = LocalizationKeys.Ui.Notification.RailEditStationInternalEdge,
            ["denied.railEdit.InvalidMode"] = LocalizationKeys.Ui.Notification.RailEditInvalidMode,
            ["denied.railEdit.NotEnoughRailItem"] = LocalizationKeys.Ui.Notification.RailEditNotEnoughRailItem,
            ["denied.railEdit.NotEnoughInventorySpace"] = LocalizationKeys.Ui.Notification.RailEditNotEnoughInventorySpace,
            ["denied.railEdit.RailLengthExceeded"] = LocalizationKeys.Ui.Notification.RailEditRailLengthExceeded,
            ["denied.railEdit.NotUnlocked"] = LocalizationKeys.Ui.Notification.RailEditNotUnlocked,
            ["denied.railEdit.UnknownError"] = LocalizationKeys.Ui.Notification.RailEditUnknownError,
            ["denied.electricWireExtend.OutOfRange"] = LocalizationKeys.Ui.Notification.ElectricWireExtendOutOfRange,
            ["denied.electricWireExtend.AlreadyConnected"] = LocalizationKeys.Ui.Notification.ElectricWireExtendAlreadyConnected,
            ["denied.electricWireExtend.ConnectionLimit"] = LocalizationKeys.Ui.Notification.ElectricWireExtendConnectionLimit,
            ["denied.electricWireExtend.NoWireItem"] = LocalizationKeys.Ui.Notification.ElectricWireExtendNoWireItem,
            ["denied.electricWireExtend.NoPoleItem"] = LocalizationKeys.Ui.Notification.ElectricWireExtendNoPoleItem,
            ["denied.electricWireExtend.InvalidTarget"] = LocalizationKeys.Ui.Notification.ElectricWireExtendInvalidTarget,
            ["denied.electricWireExtend.PositionOccupied"] = LocalizationKeys.Ui.Notification.ElectricWireExtendPositionOccupied,
            ["denied.electricWireExtend.NotUnlocked"] = LocalizationKeys.Ui.Notification.ElectricWireExtendNotUnlocked,
            ["denied.electricWireExtend.InsufficientItems"] = LocalizationKeys.Ui.Notification.ElectricWireExtendInsufficientItems,
            ["denied.electricWireExtend.InvalidMode"] = LocalizationKeys.Ui.Notification.ElectricWireExtendFailed,
            ["denied.electricWireExtend.None"] = LocalizationKeys.Ui.Notification.ElectricWireExtendFailed,
            ["denied.electricWireDisconnect.NotConnected"] = LocalizationKeys.Ui.Notification.ElectricWireDisconnectNotConnected,
            ["denied.electricWireDisconnect.InventoryFull"] = LocalizationKeys.Ui.Notification.ElectricWireDisconnectInventoryFull,
            ["denied.electricWireDisconnect.InvalidTarget"] = LocalizationKeys.Ui.Notification.ElectricWireDisconnectFailed,
            ["denied.gearChainDisconnect.NotConnected"] = LocalizationKeys.Ui.Notification.GearChainDisconnectNotConnected,
            ["denied.gearChainDisconnect.InventoryFull"] = LocalizationKeys.Ui.Notification.GearChainDisconnectInventoryFull,
            ["denied.gearChainDisconnect.InvalidTarget"] = LocalizationKeys.Ui.Notification.GearChainDisconnectFailed,
            ["denied.gearChainConnect.InvalidTarget"] = LocalizationKeys.Ui.Notification.GearChainConnectInvalidTarget,
            ["denied.gearChainConnect.NotUnlocked"] = LocalizationKeys.Ui.Notification.GearChainConnectNotUnlocked,
            ["denied.gearChainConnect.TooFar"] = LocalizationKeys.Ui.Notification.GearChainConnectTooFar,
            ["denied.gearChainConnect.AlreadyConnected"] = LocalizationKeys.Ui.Notification.GearChainConnectAlreadyConnected,
            ["denied.gearChainConnect.ConnectionLimit"] = LocalizationKeys.Ui.Notification.GearChainConnectConnectionLimit,
            ["denied.gearChainConnect.NoItem"] = LocalizationKeys.Ui.Notification.GearChainConnectNoItem,
            ["denied.blueprint.NotUnlocked"] = LocalizationKeys.Ui.Notification.BlueprintNotUnlocked,
            ["denied.blueprint.PasteCostShortage"] = LocalizationKeys.Ui.Notification.BlueprintPasteCostShortage,
            ["denied.blueprint.PasteNotUnlocked"] = LocalizationKeys.Ui.Notification.BlueprintPasteNotUnlocked,
            ["denied.blueprint.PasteLineFailed"] = LocalizationKeys.Ui.Notification.BlueprintPasteLineFailed,
            ["denied.blueprint.NotFound"] = LocalizationKeys.Ui.Notification.BlueprintNotFound,
            ["denied.blueprint.InvalidRequest"] = LocalizationKeys.Ui.Notification.BlueprintPasteInvalidRequest,
            ["denied.blueprint.PastePlacementFailed"] = LocalizationKeys.Ui.Notification.BlueprintPastePlacementFailed,
            ["denied.blueprintCreate.RequestFailed"] = LocalizationKeys.Ui.Notification.BlueprintCreateRequestFailed,
            ["denied.blueprintCreate.InvalidName"] = LocalizationKeys.Ui.Notification.BlueprintCreateInvalidName,
            ["denied.blueprintCreate.EmptyArea"] = LocalizationKeys.Ui.Notification.BlueprintCreateEmptyArea,
            ["denied.blueprintCreate.InvalidRequest"] = LocalizationKeys.Ui.Notification.BlueprintCreateInvalidRequest,
            ["denied.blueprintCreate.Unknown"] = LocalizationKeys.Ui.Notification.BlueprintCreateUnknown,
            ["saveMigration.missingMasterPruned"] = LocalizationKeys.Ui.Notification.SaveMigrationMissingMasterPruned,
        };

        // 未知の id は「不明な通知: <id>」として画面に出る（表の追加漏れはプレイヤーにも開発者にも見え、R4 の網羅テストが落ちる）
        // An unknown id shows as "Unknown notification: <id>" (a missing row is visible to players and developers, and the R4 coverage test fails)
        public static LocalizationKey Resolve(string messageId)
        {
            return Keys.TryGetValue(messageId, out var key) ? key : LocalizationKeys.Ui.Notification.UnknownMessage;
        }
    }
}
