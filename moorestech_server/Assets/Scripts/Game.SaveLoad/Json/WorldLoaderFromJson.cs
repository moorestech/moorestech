using System;
using System.Collections.Generic;
using System.IO;
using Stopwatch = System.Diagnostics.Stopwatch;
using Game.Challenge;
using Game.Context;
using Game.CraftTree;
using Game.Entity.Interface;
using Game.Map.Interface.Json;
using Game.Map.Interface.MapObject;
using Game.PlayerInventory.Interface;
using Game.PlayerRiding.Interface;
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
        private readonly RailGraphSaveLoadService _railGraphSaveLoadService;
        private readonly TrainDockingStateRestorer _trainDockingStateRestorer;
        private readonly IPlayerRidingDatastore _playerRidingDatastore;

        public WorldLoaderFromJson(SaveJsonFilePath saveJsonFilePath,
            IPlayerInventoryDataStore inventoryDataStore, IEntitiesDatastore entitiesDatastore, IWorldSettingsDatastore worldSettingsDatastore, 
            ChallengeDatastore challengeDatastore, IGameUnlockStateDataController gameUnlockStateDataController, CraftTreeManager craftTreeManager, MapInfoJson mapInfoJson,
            IResearchDataStore researchDataStore, TrainSaveLoadService trainSaveLoadService, RailGraphSaveLoadService railGraphSaveLoadService, TrainDockingStateRestorer trainDockingStateRestorer,
            IPlayerRidingDatastore playerRidingDatastore)
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
            _railGraphSaveLoadService = railGraphSaveLoadService;
            _trainDockingStateRestorer = trainDockingStateRestorer;
            _playerRidingDatastore = playerRidingDatastore;
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
            var totalStopwatch = Stopwatch.StartNew();
            WorldSaveAllInfoV1 load = null;

            // 起動時ロードの主要フェーズを計測する
            // Measure major startup load phases.
            Measure("DeserializeSaveJson", () => load = JsonConvert.DeserializeObject<WorldSaveAllInfoV1>(jsonText));
            LogSaveSummary(load, jsonText.Length);

            Measure("LoadUnlockState", () => _gameUnlockStateDataController.LoadUnlockState(load.GameUnlockStateJsonObject));
            Measure("LoadWorldBlocks", () => _worldBlockDatastore.LoadBlockDataList(load.World));
            // レールセグメントを復元する
            // Restore rail segments
            var segments = load.RailSegments ?? new List<RailSegmentSaveData>();
            Measure("RestoreRailSegments", () => _railGraphSaveLoadService.RestoreRailSegments(segments));
            Measure("LoadPlayerInventory", () => _inventoryDataStore.LoadPlayerInventory(load.Inventory));
            Measure("LoadEntities", () => _entitiesDatastore.LoadBlockDataList(load.Entities));
            Measure("LoadWorldSettings", () => _worldSettingsDatastore.LoadSettingData(load.Setting));
            Measure("LoadMapObjects", () => _mapObjectDatastore.LoadMapObject(load.MapObjects));
            Measure("LoadResearch", () => _researchDataStore.LoadResearchData(load.Research ?? new ResearchSaveJsonObject()));
            Measure("NormalizeChallenge", NormalizeChallenge);
            Measure("LoadChallenge", () => _challengeDatastore.LoadChallenge(load.Challenge));
            Measure("LoadCraftTree", () => _craftTreeManager.LoadCraftTreeInfo(load.CraftTreeInfo));
            Measure("RestoreTrainStates", () => _trainSaveLoadService.RestoreTrainStates(load.TrainUnits));
            Measure("RestoreTrainDocking", () => _trainDockingStateRestorer.RestoreDockingState());
            Measure("LoadPlayerRiding", () => _playerRidingDatastore.LoadSaveData(load.PlayerRidingStates));

            totalStopwatch.Stop();
            UnityEngine.Debug.Log($"[StartupProfile] WorldLoaderFromJson.Load totalMs={totalStopwatch.Elapsed.TotalMilliseconds:F3}");

            #region Internal

            void Measure(string name, Action action)
            {
                var stopwatch = Stopwatch.StartNew();
                action();
                stopwatch.Stop();
                UnityEngine.Debug.Log($"[StartupProfile] {name} elapsedMs={stopwatch.Elapsed.TotalMilliseconds:F3}");
            }

            void LogSaveSummary(WorldSaveAllInfoV1 saveData, int jsonLength)
            {
                // セーブ規模とフェーズ時間を同じログ群で確認する
                // Keep save size and phase timing in one log group.
                UnityEngine.Debug.Log(
                    $"[StartupProfile] SaveSummary jsonChars={jsonLength} world={saveData.World?.Count ?? 0} inventory={saveData.Inventory?.Count ?? 0} entities={saveData.Entities?.Count ?? 0} mapObjects={saveData.MapObjects?.Count ?? 0} railSegments={saveData.RailSegments?.Count ?? 0} trainUnits={saveData.TrainUnits?.Count ?? 0} playerRiding={saveData.PlayerRidingStates?.Count ?? 0}");
            }

            void NormalizeChallenge()
            {
                // Challengeがnullまたはリストがnullでないことを確認
                // Ensure Challenge and its lists are not null.
                if (load.Challenge == null)
                {
                    load.Challenge = new ChallengeJsonObject
                    {
                        CompletedGuids = new List<string>(),
                        CurrentChallengeGuids = new List<string>(),
                        PlayedSkitIds = new List<string>()
                    };
                    return;
                }

                if (load.Challenge.CompletedGuids == null) load.Challenge.CompletedGuids = new List<string>();
                if (load.Challenge.CurrentChallengeGuids == null) load.Challenge.CurrentChallengeGuids = new List<string>();
                if (load.Challenge.PlayedSkitIds == null) load.Challenge.PlayedSkitIds = new List<string>();
            }

            #endregion
        }
        
        public void WorldInitialize()
        {
            _worldSettingsDatastore.Initialize(_mapInfoJson);
            _challengeDatastore.InitializeCurrentChallenges();
        }
    }
}


