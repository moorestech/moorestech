using System.Collections.Generic;
using Core.Inventory;
using Game.PlayerInventory.Interface;
using Server.Util.MessagePack;

namespace Server.Protocol.PacketResponse.Util.RecipePlace
{
    public static class CheckPlaceableRecipe
    {
        /// <summary>
        ///     そのレシピが実際にクラフトインベントリに置けるかどうかをチェックします
        ///     メインインベントリをチェックし、アイテムが足りるかどうかをチェックします
        /// </summary>
        /// <param name="mainInventory">チェックするメインインベントリ</param>
        /// <param name="recipeItem">置こうとするレシピ</param>
        /// <returns>
        ///     isPlaceable:設置可能かどうか　mainInventoryRequiredItemCount:レシピに使用するアイテムのうち、メインインベントリにあるアイテム数 key itemId value
        ///     itemCount
        /// </returns>
        public static (bool isPlaceable, Dictionary<int, int> mainInventoryRequiredItemCount) IsPlaceable(
            IOpenableInventory mainInventory, ItemMessagePack[] recipeItem)
        {
            //必要なアイテムがMainインベントリにあるかチェックするための必要アイテム数辞書を作成
            Dictionary<int, int> requiredItemCount = CreateRequiredItemCount(recipeItem);

            //必要なアイテム数があるかチェックするためにMainインベントリを走査
            Dictionary<int, int> mainInventoryRequiredItemCount = CreateMainInventoryRequiredItemCount(mainInventory, requiredItemCount);


            //アイテム数が足りているかチェックする
            foreach (KeyValuePair<int, int> item in requiredItemCount)
            {
                if (!mainInventoryRequiredItemCount.ContainsKey(item.Key))
                    return (false, mainInventoryRequiredItemCount);

                if (mainInventoryRequiredItemCount[item.Key] < item.Value)
                    return (false, mainInventoryRequiredItemCount);
            }

            return (true, mainInventoryRequiredItemCount);
        }


        /// <summary>
        ///     レシピに必要なアイテム数の辞書を作成します
        /// </summary>
        /// <param name="recipeItem"></param>
        /// <returns>key アイテムID value アイテム数 </returns>
        private static Dictionary<int, int> CreateRequiredItemCount(ItemMessagePack[] recipeItem)
        {
            var requiredItemCount = new Dictionary<int, int>();
            foreach (var item in recipeItem)
                if (requiredItemCount.ContainsKey(item.Id))
                    requiredItemCount[item.Id] += item.Count;
                else
                    requiredItemCount.Add(item.Id, item.Count);

            return requiredItemCount;
        }


        /// <summary>
        ///     レシピに必要なアイテムがメインインベントリに何個あるかを計算します
        /// </summary>
        /// <param name="mainInventory">メインインベントリ</param>
        /// <param name="requiredItemCount">必要なアイテムIDかどうかをチェックする用</param>
        /// <returns>key アイテムID value メインインベントリに入ってる合計アイテム数</returns>
        private static Dictionary<int, int> CreateMainInventoryRequiredItemCount(IOpenableInventory mainInventory,
            IReadOnlyDictionary<int, int> requiredItemCount)
        {
            var mainInventoryRequiredItemCount = new Dictionary<int, int>();
            for (var i = 0; i < PlayerInventoryConst.MainInventorySize; i++)
            {
                var itemId = mainInventory.GetItem(i).Id;
                if (!requiredItemCount.ContainsKey(itemId)) continue;

                if (mainInventoryRequiredItemCount.ContainsKey(itemId))
                    mainInventoryRequiredItemCount[itemId] += mainInventory.GetItem(i).Count;
                else
                    mainInventoryRequiredItemCount.Add(itemId, mainInventory.GetItem(i).Count);
            }

            return mainInventoryRequiredItemCount;
        }
    }
}