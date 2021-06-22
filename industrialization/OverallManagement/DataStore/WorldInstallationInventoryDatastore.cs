using System;
using System.Collections.Generic;
using industrialization.Core.Installation;

namespace industrialization.OverallManagement.DataStore
{
    public static class WorldInstallationInventoryDatastore
    {
        private static Dictionary<string, IInstallationInventory> _installatioInventorynMasterDictionary;
        public static void AddInstallation(IInstallationInventory installation,Guid guid)
        {
            if (!_installatioInventorynMasterDictionary.ContainsKey(guid.ToString()))
            {
                _installatioInventorynMasterDictionary.Add(guid.ToString(),installation);
            }
        }

        public static IInstallationInventory GetInstallation(Guid guid)
        {
            if (_installatioInventorynMasterDictionary.ContainsKey(guid.ToString()))
            {
                return _installatioInventorynMasterDictionary[guid.ToString()];
            }

            return new NullIInstallationInventory();
        }

        public static void RemoveInstallation(IInstallationInventory installation,Guid guid)
        {
            if (_installatioInventorynMasterDictionary.ContainsKey(guid.ToString()))
            {
                _installatioInventorynMasterDictionary.Remove(guid.ToString());
            }
        }
    }
}