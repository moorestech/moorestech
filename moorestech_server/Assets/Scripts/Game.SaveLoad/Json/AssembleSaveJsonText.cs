using Game.Entity.Interface;
using Game.MapObject.Interface;
using Game.PlayerInventory.Interface;
using Game.Quest.Interface;
using Game.Save.Json.WorldVersions;
using Game.World.Interface.DataStore;
using Newtonsoft.Json;

namespace Game.Save.Json
{
    public class AssembleSaveJsonText
    {
        private readonly IEntitiesDatastore _entitiesDatastore;
        private readonly IPlayerInventoryDataStore _inventoryDataStore;
        private readonly IMapObjectDatastore _mapObjectDatastore;
        private readonly IQuestDataStore _questDataStore;
        private readonly IWorldBlockDatastore _worldBlockDatastore;
        private readonly IWorldSettingsDatastore _worldSettingsDatastore;

        public AssembleSaveJsonText(IPlayerInventoryDataStore inventoryDataStore,
            IWorldBlockDatastore worldBlockDatastore, IEntitiesDatastore entitiesDatastore,
            IQuestDataStore questDataStore, IWorldSettingsDatastore worldSettingsDatastore,
            IMapObjectDatastore mapObjectDatastore)
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
            var saveData = new WorldSaveAllInfoV1(
                _worldBlockDatastore.GetSaveBlockDataList(),
                _inventoryDataStore.GetSaveInventoryDataList(),
                _entitiesDatastore.GetSaveBlockDataList(),
                _questDataStore.GetQuestDataDictionary(),
                _worldSettingsDatastore.GetSettingsSaveData(),
                _mapObjectDatastore.GetSettingsSaveData());

            return JsonConvert.SerializeObject(saveData);
        }
    }
}