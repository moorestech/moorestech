using Client.Game.InGame.UI.Inventory.Main;
using Client.Game.InGame.UI.UIState.State;
using Client.WebUiHost.Boot;

namespace Client.WebUiHost.Game.Actions
{
    /// <summary>
    /// 在庫のアイテム移動actionを一括登録
    /// Registers the player and block inventory item-move actions together (all routed through the local inventory controller)
    /// </summary>
    internal static class InventoryWebUiActionRegistration
    {
        internal static void Register(WebSocketHub hub, LocalPlayerInventoryController controller, SubInventoryState subInventoryState)
        {
            hub.RegisterAction(new MoveItemActionHandler(controller));
            hub.RegisterAction(new SplitGrabActionHandler(controller));
            hub.RegisterAction(new SplitDragActionHandler(controller));
            hub.RegisterAction(new CollectActionHandler(controller));
            hub.RegisterAction(new SortInventoryActionHandler(controller));
            hub.RegisterAction(new BlockMoveItemActionHandler(controller, subInventoryState));
            hub.RegisterAction(new BlockSplitGrabActionHandler(controller, subInventoryState));
            hub.RegisterAction(new BlockCollectActionHandler(controller, subInventoryState));
        }
    }
}
