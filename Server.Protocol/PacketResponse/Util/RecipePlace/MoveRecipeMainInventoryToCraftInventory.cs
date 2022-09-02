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
                
                
                //必要なアイテム分を収集し、メインインベントリから消す
                CollectAndMoveItem(itemStackFactory,moveItem[i],main);

                //クラフトインベントリに入れる
                craft.SetItem(i,moveItem[i]);
            }
        }

        /// <summary>
        /// メインインベントリを0からすべてチェックし、必要なアイテム数に足りるまでアイテムを収集する
        /// </summary>
        /// <param name="itemStackFactory"></param>
        /// <param name="moveItem"></param>
        /// <param name="main"></param>
        private static void CollectAndMoveItem(ItemStackFactory itemStackFactory, IItemStack moveItem, IOpenableInventory main)
        {
            var collectedItem = itemStackFactory.CreatEmpty();
            for (int i = 0; i < PlayerInventoryConst.MainInventorySize; i++)
            {
                //必要なアイテムじゃなかったのでスルー
                if (main.GetItem(i).Id != moveItem.Id) continue;
                
                
                
                //入ってるアイテムが目的のアイテムなので収集する
                var addedItemResult = collectedItem.AddItem(main.GetItem(i));

                
                //足した結果アイテムが足りなかったらそのまま続ける
                if (addedItemResult.ProcessResultItemStack.Count < moveItem.Count)
                {
                    collectedItem = addedItemResult.ProcessResultItemStack;
                    main.SetItem(i, addedItemResult.RemainderItemStack);
                    continue;
                }
                
                

                //ピッタリだったらそのまま終了
                if (addedItemResult.ProcessResultItemStack.Count == moveItem.Count)
                {
                    main.SetItem(i, addedItemResult.RemainderItemStack);
                    break;
                }
                
                
                

                //多かったら余りをメインインベントリに戻す
                //本来入れるべきアイテムと、実際に入った数の差を計算
                var reminderCount = addedItemResult.ProcessResultItemStack.Count - moveItem.Count;
                //メインインベントリに入れる分のアイテムす　差分とあまりの数を足す
                var mainItemCount = reminderCount + addedItemResult.RemainderItemStack.Count;

                var item = itemStackFactory.Create(moveItem.Id, mainItemCount);
                main.SetItem(i, item);
            }
        }
    }
}