using Core.Item;
using Game.Challenge;
using Game.CleanRoom;
using Game.Context;
using Game.CraftTree;
using Game.Entity.Interface;
using Game.PlayerInventory.Interface;
using Game.PlayerRiding.Interface;
using Game.Research;
using Game.SaveLoad.Json.WorldVersions;
using Game.Train.SaveLoad;
using Game.UnlockState;
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
        private readonly IGameUnlockStateDataController _gameUnlockStateDataController;
        private readonly CraftTreeManager _craftTreeManager;
        private readonly IResearchDataStore _researchDataStore;
        private readonly TrainSaveLoadService _trainSaveLoadService;
        private readonly RailGraphSaveLoadService _railGraphSaveLoadService;
        private readonly IPlayerRidingDatastore _playerRidingDatastore;
        private readonly ItemStackLevelDataStore _itemStackLevelDataStore;
        private readonly IPlayerInventorySlotLevelDataStore _playerInventorySlotLevelDataStore;
        private readonly CleanRoomDatastore _cleanRoomDatastore;

        public AssembleSaveJsonText(
            IPlayerInventoryDataStore inventoryDataStore,
            IEntitiesDatastore entitiesDatastore,
            IWorldSettingsDatastore worldSettingsDatastore,
            ChallengeDatastore challengeDatastore,
            IGameUnlockStateDataController gameUnlockStateDataController,
            CraftTreeManager craftTreeManager,
            IResearchDataStore researchDataStore,
            TrainSaveLoadService trainSaveLoadService,
            RailGraphSaveLoadService railGraphSaveLoadService,
            IPlayerRidingDatastore playerRidingDatastore,
            ItemStackLevelDataStore itemStackLevelDataStore,
            IPlayerInventorySlotLevelDataStore playerInventorySlotLevelDataStore,
            CleanRoomDatastore cleanRoomDatastore)
        {
            _inventoryDataStore = inventoryDataStore;
            _entitiesDatastore = entitiesDatastore;
            _worldSettingsDatastore = worldSettingsDatastore;
            _challengeDatastore = challengeDatastore;
            _gameUnlockStateDataController = gameUnlockStateDataController;
            _craftTreeManager = craftTreeManager;
            _researchDataStore = researchDataStore;
            _trainSaveLoadService = trainSaveLoadService;
            _railGraphSaveLoadService = railGraphSaveLoadService;
            _playerRidingDatastore = playerRidingDatastore;
            _itemStackLevelDataStore = itemStackLevelDataStore;
            _playerInventorySlotLevelDataStore = playerInventorySlotLevelDataStore;
            _cleanRoomDatastore = cleanRoomDatastore;
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
                _challengeDatastore.GetSaveJsonObject(),
                _gameUnlockStateDataController.GetSaveJsonObject(),
                _craftTreeManager.GetSaveJsonObject(),
                _researchDataStore.GetSaveJsonObject(),
                _trainSaveLoadService.GetSaveJsonObject(),
                _railGraphSaveLoadService.GetSaveData(),
                _playerRidingDatastore.GetSaveData(),
                _itemStackLevelDataStore.GetSaveJsonObject(),
                _playerInventorySlotLevelDataStore.GetSaveLevel(),
                _cleanRoomDatastore.GetSaveData()
            );

            return JsonConvert.SerializeObject(saveData);
        }
    }
}
