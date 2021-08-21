using System;
using System.Collections.Generic;
using industrialization.Core.Block;

namespace industrialization.OverallManagement.DataStore
{
    public static class WorldBlockInventoryDatastore
    {
        private static Dictionary<uint, IBlockInventory> _installatioInventorynMasterDictionary = new Dictionary<uint, IBlockInventory>();
        public static void AddBlock(IBlockInventory Block,uint intId)
        {
            if (!_installatioInventorynMasterDictionary.ContainsKey(intId))
            {
                _installatioInventorynMasterDictionary.Add(intId,Block);
            }
        }

        public static IBlockInventory GetBlock(uint intId)
        {
            if (_installatioInventorynMasterDictionary.ContainsKey(intId))
            {
                return _installatioInventorynMasterDictionary[intId];
            }

            return new NullIBlockInventory();
        }

        public static void RemoveBlock(IBlockInventory Block,uint intId)
        {
            if (_installatioInventorynMasterDictionary.ContainsKey(intId))
            {
                _installatioInventorynMasterDictionary.Remove(intId);
            }
        }
    }
}