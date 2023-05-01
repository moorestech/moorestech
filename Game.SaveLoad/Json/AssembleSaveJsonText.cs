using System.Collections.Generic;
using Core.Inventory;
using Game.Entity.Interface;
using Game.MapObject.Interface;
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
        private readonly IMapObjectDatastore _mapObjectDatastore;

        public AssembleSaveJsonText(IPlayerInventoryDataStore inventoryDataStore,
            IWorldBlockDatastore worldBlockDatastore, IEntitiesDatastore entitiesDatastore, IQuestDataStore questDataStore, IWorldSettingsDatastore worldSettingsDatastore, IMapObjectDatastore mapObjectDatastore)
        {
            _inventoryDataStore = inventoryDataStore;
            _worldBlockDatastore = worldBlockDatastore;
            _entitiesDatastore = entitiesDatastore;
            _questDataStore = questDataStore;
            _worldSettingsDatastore = worldSettingsDatastore;
            _mapObjectDatastore = mapObjectDatastore;
        }

        public string AssembleSaveJson()
        {
            var saveData = new WorldSaveAllInfo(
                _worldBlockDatastore.GetSaveBlockDataList(),
                _inventoryDataStore.GetSaveInventoryDataList(),
                _entitiesDatastore.GetSaveBlockDataList(),
                _questDataStore.GetQuestDataDictionary(),
                _worldSettingsDatastore.GetSettingsSaveData(),
                _mapObjectDatastore.GetSettingsSaveData());
            
            return JsonConvert.SerializeObject(saveData);
        }
    }

    public class WorldSaveAllInfo
    {

        public WorldSaveAllInfo(List<SaveBlockData> world, List<SaveInventoryData> inventory, List<SaveEntityData> entities, Dictionary<int, List<SaveQuestData>> quests, WorldSettingSaveData setting, List<SaveMapObjectData> mapObjects)
        {
            World = world;
            Inventory = inventory;
            Entities = entities;
            Quests = quests;
            Setting = setting;
            MapObjects = mapObjects;
        }

        [JsonProperty("world")] public List<SaveBlockData> World { get; }
        [JsonProperty("playerInventory")] public List<SaveInventoryData> Inventory { get; }
        [JsonProperty("entities")] public List<SaveEntityData> Entities { get; }
        [JsonProperty("quests")] public Dictionary<int, List<SaveQuestData>> Quests { get; }
        [JsonProperty("setting")] public WorldSettingSaveData Setting { get; }
        
        [JsonProperty("mapObjects")] public List<SaveMapObjectData> MapObjects { get; set; }
    }
}