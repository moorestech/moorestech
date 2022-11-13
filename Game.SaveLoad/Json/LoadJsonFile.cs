using System;
using System.IO;
using Game.Entity.Interface;
using Game.PlayerInventory.Interface;
using Game.Quest.Interface;
using Game.Save.Interface;
using Game.World.Interface.DataStore;
using Newtonsoft.Json;

namespace Game.Save.Json
{
    public class LoadJsonFile : ILoadRepository
    {
        private readonly SaveJsonFileName _saveJsonFileName;

        private readonly IWorldBlockDatastore _worldBlockDatastore;
        private readonly IPlayerInventoryDataStore _inventoryDataStore;
        private readonly IEntitiesDatastore _entitiesDatastore;
        private readonly IQuestDataStore _questDataStore;

        public LoadJsonFile(SaveJsonFileName saveJsonFileName, IWorldBlockDatastore worldBlockDatastore,
            IPlayerInventoryDataStore inventoryDataStore, IEntitiesDatastore entitiesDatastore, IQuestDataStore questDataStore)
        {
            _saveJsonFileName = saveJsonFileName;
            _worldBlockDatastore = worldBlockDatastore;
            _inventoryDataStore = inventoryDataStore;
            _entitiesDatastore = entitiesDatastore;
            _questDataStore = questDataStore;
        }

        public void Load()
        {
            if (File.Exists(_saveJsonFileName.FullSaveFilePath))
            {
                var json = File.ReadAllText(_saveJsonFileName.FullSaveFilePath);
                try
                {
                    Load(json);
                    Console.WriteLine("セーブデータのロードが完了しました。");
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
        }

        public void Load(string jsonText)
        {
            var load = JsonConvert.DeserializeObject<SaveData>(jsonText);
            
            _worldBlockDatastore.LoadBlockDataList(load.World);
            _inventoryDataStore.LoadPlayerInventory(load.Inventory);
            _entitiesDatastore.LoadBlockDataList(load.Entities);
            _questDataStore.LoadQuestDataDictionary(load.Quests);
        }
    }
}