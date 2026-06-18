using System.Collections.Generic;
using Mooresmaster.Model.InventoryConnectsModule;
using Newtonsoft.Json;

namespace Game.Block.Blocks.BeltConveyor
{
    internal static class VanillaBeltConveyorInventorySerializer
    {
        public static void LoadItems(
            string saveState,
            VanillaBeltConveyorInventoryItem[] inventoryItems,
            InventoryConnects inventoryConnectors,
            uint ticksOfItemEnterToExit)
        {
            var itemJsons = JsonConvert.DeserializeObject<List<string>>(saveState);
            for (var i = 0; i < itemJsons.Count && i < inventoryItems.Length; i++)
            {
                if (itemJsons[i] == null) continue;
                inventoryItems[i] = VanillaBeltConveyorInventoryItem.LoadItem(itemJsons[i], inventoryConnectors, ticksOfItemEnterToExit);
            }
        }

        public static string SaveItems(VanillaBeltConveyorInventoryItem[] inventoryItems)
        {
            var saveItems = new List<string>();
            foreach (var item in inventoryItems)
            {
                saveItems.Add(item?.GetSaveJsonString());
            }

            return JsonConvert.SerializeObject(saveItems);
        }
    }
}
