using System.Collections.Generic;
using Core.Inventory;
using Game.Entity.Interface;
using Game.PlayerInventory.Interface;
using Game.Quest.Interface;
using Game.World.Interface;
using Game.World.Interface.DataStore;
using Newtonsoft.Json;

namespace Game.Save.Json
{
    public class AssembleSaveJsonText
    {
        private readonly IWorldBlockDatastore _worldBlockDatastore;
        private readonly IPlayerInventoryDataStore _inventoryDataStore;
        private readonly IEntitiesDatastore _entitiesDatastore;
        private readonly IQuestDataStore _questDataStore;

        public AssembleSaveJsonText(IPlayerInventoryDataStore inventoryDataStore,
            IWorldBlockDatastore worldBlockDatastore, IEntitiesDatastore entitiesDatastore, IQuestDataStore questDataStore)
        {
            _inventoryDataStore = inventoryDataStore;
            _worldBlockDatastore = worldBlockDatastore;
            _entitiesDatastore = entitiesDatastore;
            _questDataStore = questDataStore;
        }

        public string AssembleSaveJson()
        {
            var saveData = new SaveData(
                _worldBlockDatastore.GetSaveBlockDataList(),
                _inventoryDataStore.GetSaveInventoryDataList(),
                _entitiesDatastore.GetSaveBlockDataList(),
                _questDataStore.GetQuestDataDictionary());
            
            return JsonConvert.SerializeObject(saveData);
        }
    }

    public class SaveData
    {
        public SaveData(List<SaveBlockData> world, List<SaveInventoryData> inventory, List<SaveEntityData> entities,Dictionary<int, List<SaveQuestData>> quests)
        {
            World = world;
            Inventory = inventory;
            Entities = entities;
            Quests = quests;
        }

        [JsonProperty("world")] public List<SaveBlockData> World { get; }
        [JsonProperty("playerInventory")] public List<SaveInventoryData> Inventory { get; }
        [JsonProperty("entities")] public List<SaveEntityData> Entities { get; }
        [JsonProperty("quests")] public Dictionary<int, List<SaveQuestData>> Quests { get; }
    }
}