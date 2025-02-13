using Game.Challenge;
using Game.Context;
using Game.Entity.Interface;
using Game.PlayerInventory.Interface;
using Game.SaveLoad.Json.WorldVersions;
using Game.World.Interface.DataStore;
using Newtonsoft.Json;

namespace Game.SaveLoad.Json
{
    public class AssembleSaveJsonText
    {
        private readonly ChallengeDatastore _challengeDatastore;
        private readonly IEntitiesDatastore _entitiesDatastore;
        private readonly IPlayerInventoryDataStore _inventoryDataStore;
        private readonly IWorldSettingsDatastore _worldSettingsDatastore;
        
        public AssembleSaveJsonText(IPlayerInventoryDataStore inventoryDataStore, IEntitiesDatastore entitiesDatastore, IWorldSettingsDatastore worldSettingsDatastore, ChallengeDatastore challengeDatastore)
        {
            _inventoryDataStore = inventoryDataStore;
            _entitiesDatastore = entitiesDatastore;
            _worldSettingsDatastore = worldSettingsDatastore;
            _challengeDatastore = challengeDatastore;
        }
        
        public string AssembleSaveJson()
        {
            var worldBlockDatastore = ServerContext.WorldBlockDatastore;
            var mapObjectDatastore = ServerContext.MapObjectDatastore;
            
            var saveData = new WorldSaveAllInfoV1(
                worldBlockDatastore.GetSaveJsonObject(),
                _inventoryDataStore.GetSaveJsonObject(),
                _entitiesDatastore.GetSaveJsonObject(),
                _worldSettingsDatastore.GetSaveJsonObject(),
                mapObjectDatastore.GetSaveJsonObject(),
                _challengeDatastore.GetSaveJsonObject()
            );
            
            return JsonConvert.SerializeObject(saveData);
        }
    }
}