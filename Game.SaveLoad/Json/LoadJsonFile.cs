using System.IO;
using Game.PlayerInventory.Interface;
using Game.Save.Interface;
using Game.World.Interface.DataStore;
using Newtonsoft.Json;

namespace Game.Save.Json
{
    public class LoadJsonFile : ILoadRepository
    {
        SaveJsonFileName _saveJsonFileName;

        private IWorldBlockDatastore _worldBlockDatastore;
        private IPlayerInventoryDataStore _inventoryDataStore;

        public LoadJsonFile(SaveJsonFileName saveJsonFileName, IWorldBlockDatastore worldBlockDatastore,
            IPlayerInventoryDataStore inventoryDataStore)
        {
            _saveJsonFileName = saveJsonFileName;
            _worldBlockDatastore = worldBlockDatastore;
            _inventoryDataStore = inventoryDataStore;
        }

        public void Load()
        {
            if (File.Exists(_saveJsonFileName.FullSaveFilePath))
            {
                var json = File.ReadAllText(_saveJsonFileName.FullSaveFilePath);
                Load(json);
            }
        }

        public void Load(string jsonText)
        {
            var load = JsonConvert.DeserializeObject<SaveData>(jsonText);
            _worldBlockDatastore.LoadBlockDataList(load.World);
            _inventoryDataStore.LoadPlayerInventory(load.Inventory);
        }
    }
}