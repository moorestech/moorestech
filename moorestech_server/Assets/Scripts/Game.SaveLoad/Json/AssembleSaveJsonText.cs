using Game.Context;
using Game.Entity.Interface;
using Game.Map.Interface;
using Game.PlayerInventory.Interface;
using Game.SaveLoad.Json.WorldVersions;
using Game.World.Interface.DataStore;
using Newtonsoft.Json;

namespace Game.SaveLoad.Json
{
    public class AssembleSaveJsonText
    {
        private readonly IEntitiesDatastore _entitiesDatastore;
        private readonly IPlayerInventoryDataStore _inventoryDataStore;
        private readonly IMapObjectDatastore _mapObjectDatastore;
        private readonly IWorldSettingsDatastore _worldSettingsDatastore;

        public AssembleSaveJsonText(IPlayerInventoryDataStore inventoryDataStore, IEntitiesDatastore entitiesDatastore, IWorldSettingsDatastore worldSettingsDatastore, IMapObjectDatastore mapObjectDatastore)
        {
            _inventoryDataStore = inventoryDataStore;
            _entitiesDatastore = entitiesDatastore;
            _worldSettingsDatastore = worldSettingsDatastore;
            _mapObjectDatastore = mapObjectDatastore;
        }

        public string AssembleSaveJson()
        {
            var saveData = new WorldSaveAllInfoV1(
                ServerContext.WorldBlockDatastore.GetSaveBlockDataList(),
                _inventoryDataStore.GetSaveInventoryDataList(),
                _entitiesDatastore.GetSaveBlockDataList(),
                _worldSettingsDatastore.GetSettingsSaveData(),
                _mapObjectDatastore.GetSettingsSaveData());

            return JsonConvert.SerializeObject(saveData);
        }
    }
}