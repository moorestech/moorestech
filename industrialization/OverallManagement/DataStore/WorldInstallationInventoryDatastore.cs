using System;
using System.Collections.Generic;
using industrialization.Core.Installation;

namespace industrialization.OverallManagement.DataStore
{
    public static class WorldInstallationInventoryDatastore
    {
        private static Dictionary<int, IInstallationInventory> _installatioInventorynMasterDictionary = new();
        public static void AddInstallation(IInstallationInventory installation,int intId)
        {
            if (!_installatioInventorynMasterDictionary.ContainsKey(intId))
            {
                _installatioInventorynMasterDictionary.Add(intId,installation);
            }
        }

        public static IInstallationInventory GetInstallation(int intId)
        {
            if (_installatioInventorynMasterDictionary.ContainsKey(intId))
            {
                return _installatioInventorynMasterDictionary[intId];
            }

            return new NullIInstallationInventory();
        }

        public static void RemoveInstallation(IInstallationInventory installation,int intId)
        {
            if (_installatioInventorynMasterDictionary.ContainsKey(intId))
            {
                _installatioInventorynMasterDictionary.Remove(intId);
            }
        }
    }
}