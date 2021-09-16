using System.Collections.Generic;
using Core.Block;

namespace World.DataStore
{
    public static class WorldBlockInventoryDatastore
    {
        private static Dictionary<int, IBlockInventory> _installatioInventorynMasterDictionary = new Dictionary<int, IBlockInventory>();
        public static void AddBlock(IBlockInventory Block,int intId)
        {
            if (!_installatioInventorynMasterDictionary.ContainsKey(intId))
            {
                _installatioInventorynMasterDictionary.Add(intId,Block);
            }
        }

        public static IBlockInventory GetBlock(int intId)
        {
            if (_installatioInventorynMasterDictionary.ContainsKey(intId))
            {
                return _installatioInventorynMasterDictionary[intId];
            }

            return new NullIBlockInventory();
        }

        public static void RemoveBlock(IBlockInventory Block,int intId)
        {
            if (_installatioInventorynMasterDictionary.ContainsKey(intId))
            {
                _installatioInventorynMasterDictionary.Remove(intId);
            }
        }
    }
}