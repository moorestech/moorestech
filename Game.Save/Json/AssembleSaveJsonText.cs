using Game.PlayerInventory.Interface;
using Game.World.Interface;

namespace Game.Save.Json
{
    public class AssembleSaveJsonText
    {
        private IWorldBlockDatastore _worldBlockDatastore;
        private IPlayerInventoryDataStore _inventoryDataStore;

        public AssembleSaveJsonText(IPlayerInventoryDataStore inventoryDataStore, IWorldBlockDatastore worldBlockDatastore)
        {
            _inventoryDataStore = inventoryDataStore;
            _worldBlockDatastore = worldBlockDatastore;
        }
        
        public string AssembleSaveJson()
        {
            return "{\"world\":[],\"inventory\":[]}";
        }
    }
}