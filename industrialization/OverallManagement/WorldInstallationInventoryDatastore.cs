using System;
using System.Collections.Generic;
using industrialization.Core.Installation;
using industrialization.Core.Installation.Machine;

namespace industrialization.OverallManagement
{
    public static class WorldInstallationInventoryDatastore
    {
        private static Dictionary<Guid, IInstallationInventory> _installatioInventorynMasterDictionary;
        public static void AddInstallation(IInstallationInventory installation,Guid guid)
        {
            if (!_installatioInventorynMasterDictionary.ContainsKey(guid))
            {
                _installatioInventorynMasterDictionary.Add(guid,installation);
            }
        }

        public static IInstallationInventory GetInstallation(Guid guid)
        {
            if (_installatioInventorynMasterDictionary.ContainsKey(guid))
            {
                return _installatioInventorynMasterDictionary[guid];
            }

            return new NullIInstallationInventory();
        }

        public static void RemoveInstallation(IInstallationInventory installation,Guid guid)
        {
            if (_installatioInventorynMasterDictionary.ContainsKey(guid))
            {
                _installatioInventorynMasterDictionary.Remove(guid);
            }
        }
    }
}