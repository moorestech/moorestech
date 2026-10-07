using Client.Game.InGame.Interact;
using Client.Game.InGame.UI.Inventory;
using Client.Network.API;
using Server.Util.MessagePack;

namespace Client.Game.InGame.UI.UIState.State.SubInventory
{
    public interface ISubInventorySource
    {
        /// <summary>
        /// ブロックや列車を共通で扱えるインベントリ識別子
        /// Common inventory identifier that can handle blocks and trains
        /// </summary>
        InventoryIdentifierMessagePack InventoryIdentifier { get; }

        /// <summary>
        /// サーバー応答から開いているインベントリの真データを組み立てる
        /// Build the authoritative open-inventory data from the server response
        /// </summary>
        SubInventoryModel CreateModel(InventoryResponse inventoryResponse);

        /// <summary>
        /// 開いた世界の対象を今の表示から引き直す。離れたら閉じる判定に使い、表示が消えていればfalse
        /// Re-resolve the world target this inventory was opened from against the current view; false once that view is gone
        /// </summary>
        bool TryGetReachTarget(out IInteractable reachTarget);
    }
}
