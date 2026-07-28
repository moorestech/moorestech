using System;
using Core.Inventory;
using Core.Item.Interface;
using Game.Context;
using UnityEngine;

namespace Server.Protocol.PacketResponse.Util.InventoryService
{
    public static class InventoryItemMoveService
    {
        public static void Move(IOpenableInventory fromInventory, int fromSlot, IOpenableInventory toInventory, int toSlot, int itemCount)
        {
            try
            {
                ExecuteMove(fromInventory, fromSlot, toInventory, toSlot, itemCount);
            }
            catch (ArgumentOutOfRangeException e)
            {
                //TODO ログ基盤に入れる
                var fromInventoryName = fromInventory.GetType().Name;
                var toInventoryName = toInventory.GetType().Name;
                Debug.Log(
                    $"InventoryItemMoveService.Move: \n {e.Message} \n fromInventory={fromInventoryName} fromSlot={fromSlot} toInventory={toInventoryName} toSlot={toSlot} itemCount={itemCount}  \n {e.StackTrace}");
            }
            catch (Exception e)
            {
                Debug.Log(e);
            }
        }
        
        private static void ExecuteMove(IOpenableInventory fromInventory, int fromSlot, IOpenableInventory toInventory, int toSlot, int itemCount)
        {
            //移動元と移動先のスロットが同じ場合は移動しない
            if (fromInventory.GetHashCode() == toInventory.GetHashCode() && fromSlot == toSlot) return;
            
            
            //移動元からアイテムを取得
            var originItem = fromInventory.GetItem(fromSlot);
            //移動アイテム数が本来のアイテムより多い時は、本来のアイテム数に修正する
            if (originItem.Count < itemCount) itemCount = originItem.Count;

            var destinationInventoryItem = toInventory.GetItem(toSlot);

            //移動先アイテムがなかった時はそのまま入れかえる
            //移動先と同じIDの時は移動先スロットに加算し、余ったアイテムを移動元インベントリに入れる
            if (destinationInventoryItem.Count == 0 || originItem.Id == destinationInventoryItem.Id)
            {
                //受入制限は移動先インベントリのReplaceItemが守り、入らなかった分が余りとして返る
                //The destination inventory enforces acceptance inside ReplaceItem and returns what did not fit
                //実際に移動するアイテムインスタンスの作成
                var moveItem = ServerContext.ItemStackFactory.Create(originItem.Id, itemCount);

                //移動先インベントリにアイテムを移動
                var replaceItem = toInventory.ReplaceItem(toSlot, moveItem);

                //移動元インベントリに残るアイテムを計算
                //ゼロの時は自動でNullItemになる
                var playerItemCount = originItem.Count - itemCount;
                var addItem = ServerContext.ItemStackFactory.Create(originItem.Id, playerItemCount);

                //この分岐は移動先が空か同一IDのため、ReplaceItemの戻りは必ずmoveItem以下でありAddItemが捨てる余りは常に空になる
                //In this branch the destination is empty or the same id, so ReplaceItem returns at most moveItem and AddItem never discards anything
                var remainItem = replaceItem.AddItem(addItem).ProcessResultItemStack;
                
                //移動元インベントリに残りのアイテムをセット
                fromInventory.SetItem(fromSlot, remainItem);
            }
            //移動元と移動先のIDが異なる時、移動元インベントリのアイテムをすべて入れ替える時にのみ入れ替えを実行する
            //一部入れ替え時は入れ替え作業は実行しない
            else if (itemCount == originItem.Count)
            {
                // 入れ替えは両スロットへ書き戻すため、双方の受入制限を満たす時だけ実行する
                // A swap writes into both slots, so run it only when both sides accept the result
                if (!IsAcceptableResult(toInventory, originItem)) return;
                if (!IsAcceptableResult(fromInventory, destinationInventoryItem)) return;

                toInventory.SetItem(toSlot, originItem);
                fromInventory.SetItem(fromSlot, destinationInventoryItem);
            }

            #region Internal

            bool IsAcceptableResult(IOpenableInventory inventory, IItemStack resultItem)
            {
                // 受入制限を宣言していないインベントリと、空スロットになる結果は常に許可する
                // Inventories without restrictions and results that leave the slot empty are always allowed
                if (inventory is not IItemAcceptanceInventory acceptanceInventory) return true;
                if (resultItem.Count == 0) return true;

                if (!acceptanceInventory.CanAccept(resultItem.Id)) return false;
                return resultItem.Count <= acceptanceInventory.GetMaxCountPerSlot(resultItem.Id);
            }

            #endregion
        }
    }
}