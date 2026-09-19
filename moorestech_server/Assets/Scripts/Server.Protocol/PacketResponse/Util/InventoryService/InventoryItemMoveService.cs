using Core.Inventory;
using Game.Context;

namespace Server.Protocol.PacketResponse.Util.InventoryService
{
    public static class InventoryItemMoveService
    {
        // 拒否時は何も書き込まず、拒否の種類を返す（ログ・通知は識別子を持つプロトコル層が出す）
        // A rejection writes nothing and returns its kind; the protocol layer, which holds the identifiers, logs and notifies
        public static InventoryItemMoveResult Move(IOpenableInventory fromInventory, int fromSlot, IOpenableInventory toInventory, int toSlot, int itemCount)
        {
            //移動元と移動先のスロットが同じ場合は移動しない
            if (fromInventory.GetHashCode() == toInventory.GetHashCode() && fromSlot == toSlot) return InventoryItemMoveResult.NoOp;
            
            
            //移動元からアイテムを取得
            var originItem = fromInventory.GetItem(fromSlot);
            //移動アイテム数が本来のアイテムより多い時は、本来のアイテム数に修正する
            if (originItem.Count < itemCount) itemCount = originItem.Count;
            
            //実際に移動するアイテムインスタンスの作成
            var moveItem = ServerContext.ItemStackFactory.Create(originItem.Id, itemCount);
            
            var destinationInventoryItem = toInventory.GetItem(toSlot);
            
            //移動先アイテムがなかった時はそのまま入れかえる
            //移動先と同じIDの時は移動先スロットに加算し、余ったアイテムを移動元インベントリに入れる
            if (destinationInventoryItem.Count == 0 || originItem.Id == destinationInventoryItem.Id)
            {
                // 移動先が受け入れない時は書き込まずに拒否を返す
                // Return a rejection without writing when the destination refuses the stack
                if (!toInventory.IsAllowedToPlace(toSlot, moveItem)) return InventoryItemMoveResult.RejectedByDestination;

                //移動先インベントリにアイテムを移動
                var replaceItem = toInventory.ReplaceItem(toSlot, moveItem);
                
                //移動元インベントリに残るアイテムを計算
                //ゼロの時は自動でNullItemになる
                var playerItemCount = originItem.Count - itemCount;
                var addItem = ServerContext.ItemStackFactory.Create(originItem.Id, playerItemCount);
                var remainItem = replaceItem.AddItem(addItem).ProcessResultItemStack;
                
                //移動元インベントリに残りのアイテムをセット
                fromInventory.SetItem(fromSlot, remainItem);
                return InventoryItemMoveResult.Moved;
            }

            // 異なるアイテムの一部だけの入れ替えは行わない
            // A partial swap between different items is not performed
            if (itemCount != originItem.Count) return InventoryItemMoveResult.RejectedPartialSwap;

            // 両側が書き込みを受け入れるか先に確認し、片方だけ書く複製・消失を防ぐ
            // Confirm both sides accept the write first to avoid a one-sided write that duplicates or loses items
            if (!toInventory.IsAllowedToPlace(toSlot, originItem)) return InventoryItemMoveResult.RejectedByDestination;
            if (!fromInventory.IsAllowedToPlace(fromSlot, destinationInventoryItem)) return InventoryItemMoveResult.RejectedBySource;

            toInventory.SetItem(toSlot, originItem);
            fromInventory.SetItem(fromSlot, destinationInventoryItem);
            return InventoryItemMoveResult.Moved;
        }
    }
}
