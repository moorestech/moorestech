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
        private readonly IWorldSettingsDatastore _worldSettingsDatastore;

        public AssembleSaveJsonText(IPlayerInventoryDataStore inventoryDataStore,
            IWorldBlockDatastore worldBlockDatastore, IEntitiesDatastore entitiesDatastore, IQuestDataStore questDataStore, IWorldSettingsDatastore worldSettingsDatastore)
        {
            _inventoryDataStore = inventoryDataStore;
            _worldBlockDatastore = worldBlockDatastore;
            _entitiesDatastore = entitiesDatastore;
            _questDataStore = questDataStore;
            _worldSettingsDatastore = worldSettingsDatastore;
        }

        public string AssembleSaveJson()
        {
            var saveData = new WorldSaveAllInfo(
                _worldBlockDatastore.GetSaveBlockDataList(),
                _inventoryDataStore.GetSaveInventoryDataList(),
                _entitiesDatastore.GetSaveBlockDataList(),
                _questDataStore.GetQuestDataDictionary(),
                _worldSettingsDatastore.GetSettingsSaveData());
            
            return JsonConvert.SerializeObject(saveData);
        }
    }

    public class WorldSaveAllInfo
    {

        public WorldSaveAllInfo(List<SaveBlockData> world, List<SaveInventoryData> inventory, List<SaveEntityData> entities, Dictionary<int, List<SaveQuestData>> quests, WorldSettingSaveData setting)
        {
            World = world;
            Inventory = inventory;
            Entities = entities;
            Quests = quests;
            Setting = setting;
        }

        [JsonProperty("world")] public List<SaveBlockData> World { get; }
        [JsonProperty("playerInventory")] public List<SaveInventoryData> Inventory { get; }
        [JsonProperty("entities")] public List<SaveEntityData> Entities { get; }
        [JsonProperty("quests")] public Dictionary<int, List<SaveQuestData>> Quests { get; }
        [JsonProperty("setting")] public WorldSettingSaveData Setting { get; }
    }
}