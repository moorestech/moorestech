using Core.Const;
using Core.Inventory;
using Core.Item;
using Game.PlayerInventory.Interface;

namespace Server.Protocol.PacketResponse.Util.RecipePlace
{
    public class MoveRecipeMainInventoryToCraftInventory
    {
        /// <summary>
        /// 実際にアイテムを移動する処理
        /// クラフトインベントリに置くアイテム数をもとにメインインベントリからアイテムを収集し、アイテムを消す
        /// クラフトインベントリにアイテムをセットする
        /// </summary>
        /// <param name="itemStackFactory">アイテム作成用</param>
        /// <param name="main">収集元となるインベントリ</param>
        /// <param name="craft">設置先となるインベントリ</param>
        /// <param name="moveItem">実際に移動するアイテム</param>
        public static void Move(ItemStackFactory itemStackFactory,IOpenableInventory main,IOpenableInventory craft,IItemStack[] moveItem)
        {
            for (int i = 0; i < PlayerInventoryConst.CraftingSlotSize; i++)
            {
                //メインインベントリから欲しいアイテムを収集してくる
                if (moveItem[i].Id == ItemConst.EmptyItemId)  continue; 
                
                var collectedItem = itemStackFactory.CreatEmpty();
                for (int j = 0; j < PlayerInventoryConst.MainInventorySize; j++)
                {
                    if (main.GetItem(j).Id != moveItem[i].Id) continue;
                    //入ってるアイテムが目的のアイテムなので収集する
                    var addedItemResult = collectedItem.AddItem(main.GetItem(i));
                    
                    //足した結果アイテムが足りなかったらそのまま続ける
                    if (addedItemResult.ProcessResultItemStack.Count < moveItem[i].Count)
                    {
                        collectedItem = addedItemResult.ProcessResultItemStack;
                        main.SetItem(j,addedItemResult.RemainderItemStack);
                        continue;
                    }
                    //ピッタリだったらそのまま終了
                    if (addedItemResult.ProcessResultItemStack.Count == moveItem[i].Count)
                    {
                        main.SetItem(j,addedItemResult.RemainderItemStack);
                        break;
                    }
                    
                    //多かったら余りをメインインベントリに戻す
                    //本来入れるべきアイテムと、実際に入った数の差を計算
                    var reminderCount = addedItemResult.ProcessResultItemStack.Count - moveItem[i].Count;
                    //メインインベントリに入れる分のアイテムす　差分とあまりの数を足す
                    var mainItemCount = reminderCount + addedItemResult.RemainderItemStack.Count;
                    
                    var item = itemStackFactory.Create(moveItem[i].Id, mainItemCount);
                    main.SetItem(j,item);
                }
                
                
                //クラフトインベントリに入れる
                craft.SetItem(i,moveItem[i]);
            }
        }
    }
}