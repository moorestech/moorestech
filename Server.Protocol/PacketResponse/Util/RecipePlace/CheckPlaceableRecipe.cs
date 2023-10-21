using System.Collections.Generic;
using Core.Inventory;
using Game.PlayerInventory.Interface;
using Server.Util.MessagePack;

namespace Server.Protocol.PacketResponse.Util.RecipePlace
{
    public static class CheckPlaceableRecipe
    {

        ///     
        ///     

        /// <param name="mainInventory"></param>
        /// <param name="recipeItem"></param>
        /// <returns>
        ///     isPlaceable:　mainInventoryRequiredItemCount: key itemId value
        ///     itemCount
        /// </returns>
        public static (bool isPlaceable, Dictionary<int, int> mainInventoryRequiredItemCount) IsPlaceable(IOpenableInventory mainInventory, ItemMessagePack[] recipeItem)
        {
            //Main
            var requiredItemCount = CreateRequiredItemCount(recipeItem);

            //Main
            var mainInventoryRequiredItemCount = CreateMainInventoryRequiredItemCount(mainInventory, requiredItemCount);


            
            foreach (var item in requiredItemCount)
            {
                if (!mainInventoryRequiredItemCount.ContainsKey(item.Key)) return (false, mainInventoryRequiredItemCount);

                if (mainInventoryRequiredItemCount[item.Key] < item.Value) return (false, mainInventoryRequiredItemCount);
            }

            return (true, mainInventoryRequiredItemCount);
        }



        ///     

        /// <param name="recipeItem"></param>
        /// <returns>key ID value  </returns>
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



        ///     

        /// <param name="mainInventory"></param>
        /// <param name="requiredItemCount">ID</param>
        /// <returns>key ID value </returns>
        private static Dictionary<int, int> CreateMainInventoryRequiredItemCount(IOpenableInventory mainInventory, IReadOnlyDictionary<int, int> requiredItemCount)
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