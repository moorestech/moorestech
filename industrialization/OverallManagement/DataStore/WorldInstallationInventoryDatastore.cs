using System;
using System.Collections.Generic;
using industrialization.Core.Block;

namespace industrialization.OverallManagement.DataStore
{
    public static class WorldBlockInventoryDatastore
    {
        private static Dictionary<int, IBlockInventory> _installatioInventorynMasterDictionary = new();
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