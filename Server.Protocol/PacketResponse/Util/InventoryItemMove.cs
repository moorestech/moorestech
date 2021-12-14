using Core.Inventory;
using Core.Item;
using Core.Item.Util;

namespace Server.PacketHandle.PacketResponse.Util
{
    public class InventoryItemMove
    {
        public void Move(ItemStackFactory itemStackFactory,IInventory sourceInventory,int sourceSlot,IInventory destinationInventory,int destinationSlot,int itemAmount)
        {
            //移動元からアイテムを取得
            var originItem = sourceInventory.GetItem(sourceSlot);
            //動かすアイテム数の修正
            if (originItem.Amount < itemAmount)
            {
                itemAmount = originItem.Amount;
            }
            //実際に移動するアイテムインスタンスの作成
            var moveItem = itemStackFactory.Create(originItem.Id,itemAmount);
                
            var destinationInventoryItem = destinationInventory.GetItem(destinationSlot);
                
            //移動先アイテムがなかった時はそのまま入れかえる
            //移動先と同じIDの時は移動先スロットに加算し、余ったアイテムを移動元インベントリに入れる
            if (destinationInventoryItem.Amount == 0 || originItem.Id == destinationInventoryItem.Id)
            {
                //移動先インベントリにアイテムを移動
                var replaceItem = destinationInventory.ReplaceItem(destinationSlot,moveItem);
                
                //移動元インベントリに残るアイテムを計算
                //ゼロの時は自動でNullItemになる
                var playerItemAmount = originItem.Amount - itemAmount;
                var remainItem = replaceItem.AddItem(itemStackFactory.Create(originItem.Id,playerItemAmount)).ProcessResultItemStack;
                    
                //移動元インベントリに残りのアイテムをセット
                sourceInventory.SetItem(sourceSlot,remainItem);
            }
            else if (itemAmount == originItem.Amount)
            {
                //移動元インベントリのアイテムをすべて入れ替える時にのみ入れ替えを実行する
                //一部入れ替え時は入れ替え作業は実行しない
                destinationInventory.SetItem(destinationSlot,originItem);
                sourceInventory.SetItem(sourceSlot,destinationInventoryItem);
            }
        }
        
    }
}