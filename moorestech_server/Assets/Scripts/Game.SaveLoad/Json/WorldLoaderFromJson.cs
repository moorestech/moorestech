using System;
using System.IO;
using Game.Challenge;
using Game.Context;
using Game.Entity.Interface;
using Game.Map.Interface.MapObject;
using Game.PlayerInventory.Interface;
using Game.SaveLoad.Interface;
using Game.SaveLoad.Json.WorldVersions;
using Game.World.Interface.DataStore;
using Newtonsoft.Json;
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
        
        private readonly SaveJsonFileName _saveJsonFileName;
        private readonly IWorldBlockDatastore _worldBlockDatastore;
        private readonly IWorldSettingsDatastore _worldSettingsDatastore;
        
        public WorldLoaderFromJson(SaveJsonFileName saveJsonFileName,
            IPlayerInventoryDataStore inventoryDataStore, IEntitiesDatastore entitiesDatastore, IWorldSettingsDatastore worldSettingsDatastore, ChallengeDatastore challengeDatastore)
        {
            _worldBlockDatastore = ServerContext.WorldBlockDatastore;
            _mapObjectDatastore = ServerContext.MapObjectDatastore;
            
            _saveJsonFileName = saveJsonFileName;
            _inventoryDataStore = inventoryDataStore;
            _entitiesDatastore = entitiesDatastore;
            _worldSettingsDatastore = worldSettingsDatastore;
            _challengeDatastore = challengeDatastore;
        }
        
        public void LoadOrInitialize()
        {
            if (File.Exists(_saveJsonFileName.FullSaveFilePath))
            {
                var json = File.ReadAllText(_saveJsonFileName.FullSaveFilePath);
                try
                {
                    Load(json);
                    Debug.Log("セーブデータのロードが完了しました。");
                    return;
                }
                catch (Exception e)
                {
                    //TODO ログ基盤
                    Debug.Log("セーブデータが破損していたか古いバージョンでした。Discordサーバー ( https://discord.gg/ekFYmY3rDP ) にて連絡をお願いします。");
                    Debug.Log($"セーブファイルパス {_saveJsonFileName.FullSaveFilePath}");
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
            
            _worldBlockDatastore.LoadBlockDataList(load.World);
            _inventoryDataStore.LoadPlayerInventory(load.Inventory);
            _entitiesDatastore.LoadBlockDataList(load.Entities);
            _worldSettingsDatastore.LoadSettingData(load.Setting);
            _mapObjectDatastore.LoadMapObject(load.MapObjects);
            _challengeDatastore.LoadChallenge(load.Challenge);
        }
        
        public void WorldInitialize()
        {
            _worldSettingsDatastore.Initialize();
        }
    }
}