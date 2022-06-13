using System.Collections.Generic;
using Core.Inventory;
using Game.Entity.Interface;
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
        private IEntitiesDatastore _entitiesDatastore;

        public AssembleSaveJsonText(IPlayerInventoryDataStore inventoryDataStore,
            IWorldBlockDatastore worldBlockDatastore, IEntitiesDatastore entitiesDatastore)
        {
            _inventoryDataStore = inventoryDataStore;
            _worldBlockDatastore = worldBlockDatastore;
            _entitiesDatastore = entitiesDatastore;
        }

        public string AssembleSaveJson()
        {
            var saveData = new SaveData(
                _worldBlockDatastore.GetSaveBlockDataList(),
                _inventoryDataStore.GetSaveInventoryDataList(),
                _entitiesDatastore.GetSaveBlockDataList());
            
            return JsonConvert.SerializeObject(saveData);
        }
    }

    public class SaveData
    {
        public SaveData(List<SaveBlockData> world, List<SaveInventoryData> inventory, List<SaveEntityData> entities)
        {
            World = world;
            Inventory = inventory;
            Entities = entities;
        }

        [JsonProperty("world")] public List<SaveBlockData> World { get; }
        [JsonProperty("playerInventory")] public List<SaveInventoryData> Inventory { get; }
        [JsonProperty("entities")] public List<SaveEntityData> Entities { get; }
    }
}