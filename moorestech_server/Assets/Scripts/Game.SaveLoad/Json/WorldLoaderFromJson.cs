using System;
using System.IO;
using Game.Challenge;
using Game.Context;
using Game.CraftTree;
using Game.Entity.Interface;
using Game.Map.Interface.Json;
using Game.Map.Interface.MapObject;
using Game.PlayerInventory.Interface;
using Game.SaveLoad.Interface;
using Game.SaveLoad.Json.WorldVersions;
using Game.Research;
using Game.UnlockState;
using Game.World.Interface.DataStore;
using Newtonsoft.Json;
using Game.Train.SaveLoad;
using Game.Train.Unit;
using UnityEngine;

namespace Game.SaveLoad.Json
{
    public class WorldLoaderFromJson : IWorldSaveDataLoader
    {
        private readonly ChallengeDatastore _challengeDatastore;
        private readonly ChallengeJsonObject _challengeJsonObject;
        private readonly IEntitiesDatastore _entitiesDatastore;
        private readonly IPlayerInventoryDataStore _inventoryDataStore;
        private readonly IMapObjectDatastore _mapObjectDatastore;
        private readonly MapInfoJson _mapInfoJson;
        
        private readonly SaveJsonFilePath _saveJsonFilePath;
        private readonly IWorldBlockDatastore _worldBlockDatastore;
        private readonly IWorldSettingsDatastore _worldSettingsDatastore;
        private readonly IGameUnlockStateDataController _gameUnlockStateDataController;
        private readonly CraftTreeManager _craftTreeManager;
        private readonly IResearchDataStore _researchDataStore;
        private readonly TrainSaveLoadService _trainSaveLoadService;
        private readonly TrainDockingStateRestorer _trainDockingStateRestorer;

        public WorldLoaderFromJson(SaveJsonFilePath saveJsonFilePath,
            IPlayerInventoryDataStore inventoryDataStore, IEntitiesDatastore entitiesDatastore, IWorldSettingsDatastore worldSettingsDatastore, 
            ChallengeDatastore challengeDatastore, IGameUnlockStateDataController gameUnlockStateDataController, CraftTreeManager craftTreeManager, MapInfoJson mapInfoJson,
            IResearchDataStore researchDataStore, TrainSaveLoadService trainSaveLoadService, TrainDockingStateRestorer trainDockingStateRestorer)
        {
            _worldBlockDatastore = ServerContext.WorldBlockDatastore;
            _mapObjectDatastore = ServerContext.MapObjectDatastore;

            _saveJsonFilePath = saveJsonFilePath;
            _inventoryDataStore = inventoryDataStore;
            _entitiesDatastore = entitiesDatastore;
            _worldSettingsDatastore = worldSettingsDatastore;
            _challengeDatastore = challengeDatastore;
            _gameUnlockStateDataController = gameUnlockStateDataController;
            _craftTreeManager = craftTreeManager;
            _mapInfoJson = mapInfoJson;
            _researchDataStore = researchDataStore;
            _trainSaveLoadService = trainSaveLoadService;
            _trainDockingStateRestorer = trainDockingStateRestorer;
        }
        
        public void LoadOrInitialize()
        {
            if (File.Exists(_saveJsonFilePath.Path))
            {
                var json = File.ReadAllText(_saveJsonFilePath.Path);
                try
                {
                    Load(json);
                    Debug.Log("セーブデータのロードが完了しました。");
                    return;
                }
                catch (Exception e)
                {
                    //TODO ログ基盤
                    Debug.Log("セーブデータが破損していたか古いバージョンでした。削除したら治る可能性があります。\nサポートが必要な場合はDiscordサーバー ( https://discord.gg/ekFYmY3rDP ) にて連絡をお願いします。");
                    Debug.Log($"セーブファイルパス {_saveJsonFilePath.Path}");
                    throw new Exception(
                        $"セーブファイルのロードに失敗しました。セーブファイルを確認してください。\n Message : {e.Message} \n StackTrace : {e.StackTrace}");
                }
            }
            
            Debug.Log("セーブデータがありませんでした。新規作成します。");
            WorldInitialize();
        }
        
        public void Load(string jsonText)
        {
            var load = JsonConvert.DeserializeObject<WorldSaveAllInfoV1>(jsonText);
            
            _gameUnlockStateDataController.LoadUnlockState(load.GameUnlockStateJsonObject);
            _worldBlockDatastore.LoadBlockDataList(load.World);
            _inventoryDataStore.LoadPlayerInventory(load.Inventory);
            _entitiesDatastore.LoadBlockDataList(load.Entities);
            _worldSettingsDatastore.LoadSettingData(load.Setting);
            _mapObjectDatastore.LoadMapObject(load.MapObjects);
            _researchDataStore.LoadResearchData(load.Research ?? new ResearchSaveJsonObject());
            
            // Challengeがnullまたはリストがnullでないことを確認
            if (load.Challenge == null)
            {
                load.Challenge = new ChallengeJsonObject
                {
                    CompletedGuids = new System.Collections.Generic.List<string>(),
                    CurrentChallengeGuids = new System.Collections.Generic.List<string>(),
                    PlayedSkitIds = new System.Collections.Generic.List<string>()
                };
            }
            else
            {
                if (load.Challenge.CompletedGuids == null)
                    load.Challenge.CompletedGuids = new System.Collections.Generic.List<string>();
                if (load.Challenge.CurrentChallengeGuids == null)
                    load.Challenge.CurrentChallengeGuids = new System.Collections.Generic.List<string>();
                if (load.Challenge.PlayedSkitIds == null)
                    load.Challenge.PlayedSkitIds = new System.Collections.Generic.List<string>();
            }
            
            _challengeDatastore.LoadChallenge(load.Challenge);
            _craftTreeManager.LoadCraftTreeInfo(load.CraftTreeInfo);

            _trainSaveLoadService.RestoreTrainStates(load.TrainUnits);
            _trainDockingStateRestorer.RestoreDockingState();
        }
        
        public void WorldInitialize()
        {
            _worldSettingsDatastore.Initialize(_mapInfoJson);
            _challengeDatastore.InitializeCurrentChallenges();
        }
    }
}


