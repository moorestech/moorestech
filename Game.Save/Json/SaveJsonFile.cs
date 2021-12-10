using System;
using Game.PlayerInventory.Interface;
using Game.Save.Interface;
using Game.World.Interface;

namespace Game.Save.Json
{
    public class SaveJsonFile : ISaveRepository
    {
        private IWorldBlockDatastore _worldBlockDatastore;
        private IPlayerInventoryDataStore _inventoryDataStore;
        private string _filePath;

        public SaveJsonFile(string filePath, IPlayerInventoryDataStore inventoryDataStore, IWorldBlockDatastore worldBlockDatastore)
        {
            _filePath = filePath;
            _inventoryDataStore = inventoryDataStore;
            _worldBlockDatastore = worldBlockDatastore;
        }

        public void Save()
        {
            throw new System.NotImplementedException();
        }
    }
}