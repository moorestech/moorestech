using System.Collections.Generic;
using Core.Inventory;
using Game.PlayerInventory.Interface;
using Game.World.Interface;
using Game.World.Interface.DataStore;
using Newtonsoft.Json;

namespace Game.Save.Json
{
    public class AssembleSaveJsonText
    {
        private IWorldBlockDatastore _worldBlockDatastore;
        private IPlayerInventoryDataStore _inventoryDataStore;

        public AssembleSaveJsonText(IPlayerInventoryDataStore inventoryDataStore,
            IWorldBlockDatastore worldBlockDatastore)
        {
            _inventoryDataStore = inventoryDataStore;
            _worldBlockDatastore = worldBlockDatastore;
        }

        public string AssembleSaveJson()
        {
            var saveData = new SaveData(_worldBlockDatastore.GetSaveBlockDataList(),
                _inventoryDataStore.GetSaveInventoryDataList());
            return JsonConvert.SerializeObject(saveData);
        }

        public void LoadJson(string json)
        {
            var load = JsonConvert.DeserializeObject<SaveData>(json);
            _worldBlockDatastore.LoadBlockDataList(load.World);
        }
    }

    public class SaveData
    {
        public SaveData(List<SaveBlockData> world, List<SaveInventoryData> inventory)
        {
            World = world;
            Inventory = inventory;
        }

        [JsonProperty("world")] public List<SaveBlockData> World { get; }
        [JsonProperty("playerInventory")] public List<SaveInventoryData> Inventory { get; }
    }
}