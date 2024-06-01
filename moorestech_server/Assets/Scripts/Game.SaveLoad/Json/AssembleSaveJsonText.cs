using Game.Challenge;
using Game.Context;
using Game.Entity.Interface;
using Game.Map.Interface;
using Game.Map.Interface.MapObject;
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
        private readonly ChallengeDatastore _challengeDatastore;

        public AssembleSaveJsonText(IPlayerInventoryDataStore inventoryDataStore, IEntitiesDatastore entitiesDatastore, IWorldSettingsDatastore worldSettingsDatastore, IMapObjectDatastore mapObjectDatastore, ChallengeDatastore challengeDatastore)
        {
            _inventoryDataStore = inventoryDataStore;
            _entitiesDatastore = entitiesDatastore;
            _worldSettingsDatastore = worldSettingsDatastore;
            _mapObjectDatastore = mapObjectDatastore;
            _challengeDatastore = challengeDatastore;
        }

        public string AssembleSaveJson()
        {
            var saveData = new WorldSaveAllInfoV1(
                ServerContext.WorldBlockDatastore.GetSaveJsonObject(),
                _inventoryDataStore.GetSaveJsonObject(),
                _entitiesDatastore.GetSaveJsonObject(),
                _worldSettingsDatastore.GetSaveJsonObject(),
                _mapObjectDatastore.GetSaveJsonObject(),
                _challengeDatastore.GetSaveJsonObject()
                );

            return JsonConvert.SerializeObject(saveData);
        }
    }
}