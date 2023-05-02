using System;
using System.Collections.Generic;
using System.IO;
using Game.Entity.Interface;
using Game.MapObject.Interface;
using Game.MapObject.Interface.Json;
using Game.PlayerInventory.Interface;
using Game.Quest.Interface;
using Game.Save.Interface;
using Game.World.Interface.DataStore;
using Newtonsoft.Json;

namespace Game.Save.Json
{
    public class WorldLoaderFromJson : IWorldSaveDataLoader
    {
        private readonly SaveJsonFileName _saveJsonFileName;
        private readonly MapConfigFileName _mapConfigFileName;

        private readonly IWorldBlockDatastore _worldBlockDatastore;
        private readonly IPlayerInventoryDataStore _inventoryDataStore;
        private readonly IEntitiesDatastore _entitiesDatastore;
        private readonly IQuestDataStore _questDataStore;
        private readonly IWorldSettingsDatastore _worldSettingsDatastore;
        private readonly IMapObjectDatastore _mapObjectDatastore;

        public WorldLoaderFromJson(SaveJsonFileName saveJsonFileName, IWorldBlockDatastore worldBlockDatastore,
            IPlayerInventoryDataStore inventoryDataStore, IEntitiesDatastore entitiesDatastore, IQuestDataStore questDataStore, IWorldSettingsDatastore worldSettingsDatastore, IMapObjectDatastore mapObjectDatastore, MapConfigFileName mapConfigFileName)
        {
            _saveJsonFileName = saveJsonFileName;
            _worldBlockDatastore = worldBlockDatastore;
            _inventoryDataStore = inventoryDataStore;
            _entitiesDatastore = entitiesDatastore;
            _questDataStore = questDataStore;
            _worldSettingsDatastore = worldSettingsDatastore;
            _mapObjectDatastore = mapObjectDatastore;
            _mapConfigFileName = mapConfigFileName;
        }

        public void LoadOrInitialize()
        {
            if (File.Exists(_saveJsonFileName.FullSaveFilePath))
            {
                var json = File.ReadAllText(_saveJsonFileName.FullSaveFilePath);
                try
                {
                    Load(json);
                    Console.WriteLine("セーブデータのロードが完了しました。");
                    return;
                }
                catch (Exception e)
                {
                    Console.WriteLine("セーブデータが破損していたか古いバージョンでした。新しく作成します。");
                    //throw new Exception($"セーブファイルのロードに失敗しました。セーブファイルを確認してください。\n Message : {e.Message} \n StackTrace : {e.StackTrace}");
                }
            }
            else
            {
                Console.WriteLine("セーブデータがありませんでした。新規作成します。");
            }
            WorldInitialize();
        }

        public void Load(string jsonText)
        {
            var load = JsonConvert.DeserializeObject<WorldSaveAllInfo>(jsonText);
            
            _worldBlockDatastore.LoadBlockDataList(load.World);
            _inventoryDataStore.LoadPlayerInventory(load.Inventory);
            _entitiesDatastore.LoadBlockDataList(load.Entities);
            _questDataStore.LoadQuestDataDictionary(load.Quests);
            _worldSettingsDatastore.LoadSettingData(load.Setting);
            _mapObjectDatastore.LoadObject(load.MapObjects);
        }

        private void WorldInitialize()
        {
            _worldSettingsDatastore.Initialize();
            var mapObjects = JsonConvert.DeserializeObject<List<ConfigMapObjectData>>(File.ReadAllText(_mapConfigFileName.FullMapObjectConfigFilePath));
            _mapObjectDatastore.InitializeObject(mapObjects);
        }
    }
}