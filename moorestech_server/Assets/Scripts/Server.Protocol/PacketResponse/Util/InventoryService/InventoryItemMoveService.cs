using System;
using Core.Inventory;
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

            //実際に移動するアイテムインスタンスの作成
            var moveItem = ServerContext.ItemStackFactory.Create(originItem.Id, itemCount);

            var destinationInventoryItem = toInventory.GetItem(toSlot);

            //移動先アイテムがなかった時はそのまま入れかえる
            //移動先と同じIDの時は移動先スロットに加算し、余ったアイテムを移動元インベントリに入れる
            if (destinationInventoryItem.Count == 0 || originItem.Id == destinationInventoryItem.Id)
            {
                //移動先インベントリにアイテムを移動
                var replaceItem = toInventory.ReplaceItem(toSlot, moveItem);

                //移動元インベントリに残るアイテムを計算
                //ゼロの時は自動でNullItemになる
                var playerItemCount = originItem.Count - itemCount;
                var addItem = ServerContext.ItemStackFactory.Create(originItem.Id, playerItemCount);
                var remainItem = replaceItem.AddItem(addItem).ProcessResultItemStack;

                //移動元インベントリに残りのアイテムをセット
                fromInventory.SetItem(fromSlot, remainItem);
            }
            //移動元と移動先のIDが異なる時、移動元インベントリのアイテムをすべて入れ替える時にのみ入れ替えを実行する
            //一部入れ替え時は入れ替え作業は実行しない
            else if (itemCount == originItem.Count)
            {
                toInventory.SetItem(toSlot, originItem);
                fromInventory.SetItem(fromSlot, destinationInventoryItem);
            }
        }
    }
}