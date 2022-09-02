using System.Collections.Generic;
using Core.Inventory;
using Game.PlayerInventory.Interface;
using Server.Util.MessagePack;

namespace Server.Protocol.PacketResponse.Util.RecipePlace
{
    public class CheckPlaceableRecipe
    {
        /// <summary>
        /// そのレシピが実際にクラフトインベントリに置けるかどうかをチェックします
        /// メインインベントリをチェックし、アイテムが足りるかどうかをチェックします
        /// </summary>
        /// <param name="mainInventory">チェックするメインインベントリ</param>
        /// <param name="recipeItem">置こうとするレシピ</param>
        /// <returns>isPlaceable:設置可能かどうか　mainInventoryRequiredItemCount:レシピに使用するアイテムのうち、メインインベントリにあるアイテム数 key itemId value itemCount</returns>
        public static (bool isPlaceable, Dictionary<int, int> mainInventoryRequiredItemCount) IsPlaceable(IOpenableInventory mainInventory,ItemMessagePack[] recipeItem)
        {
            //必要なアイテムがMainインベントリにあるかチェックするための必要アイテム数辞書を作成
            var requiredItemCount = new Dictionary<int, int>();
            foreach (var item in recipeItem)
            {
                if (requiredItemCount.ContainsKey(item.Id))
                {
                    requiredItemCount[item.Id] += item.Count;
                }
                else
                {
                    requiredItemCount.Add(item.Id, item.Count);
                }
            }
            //必要なアイテム数があるかチェックするためにMainインベントリを走査
            var mainInventoryRequiredItemCount = new Dictionary<int, int>();
            for (int i = 0; i < PlayerInventoryConst.MainInventorySize; i++)
            {
                var itemId = mainInventory.GetItem(i).Id;
                if (!requiredItemCount.ContainsKey(itemId)) continue;

                if (mainInventoryRequiredItemCount.ContainsKey(itemId))
                {
                    mainInventoryRequiredItemCount[itemId] += mainInventory.GetItem(i).Count;
                }
                else
                {
                    mainInventoryRequiredItemCount.Add(itemId, mainInventory.GetItem(i).Count);
                }
            }
            
            //アイテム数が足りているかチェックする
            foreach (var item in requiredItemCount)
            {
                if (!mainInventoryRequiredItemCount.ContainsKey(item.Key)) return (false,null);
                
                if (mainInventoryRequiredItemCount[item.Key] < item.Value)
                {
                    return (false,null);
                }
            }
            return (true,mainInventoryRequiredItemCount);
        }
    }
}