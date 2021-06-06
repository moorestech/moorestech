using System;
using System.Collections.Generic;
using industrialization.Core.Installation;

namespace industrialization.OverallManagement
{
    public static class WorldInstallationDatastore
    {
        private static Dictionary<Guid, InstallationBase> _installationMasterDictionary;

        public static void AddInstallation(InstallationBase installation)
        {
            if (!_installationMasterDictionary.ContainsKey(installation.Guid))
            {
                _installationMasterDictionary.Add(installation.Guid,installation);
            }
        }

        public static InstallationBase GetInstallation(Guid guid)
        {
            if (_installationMasterDictionary.ContainsKey(guid))
            {
                return _installationMasterDictionary[guid];
            }

            return null;
        }

        public static void RemoveInstallation(InstallationBase installation)
        {
            if (_installationMasterDictionary.ContainsKey(installation.Guid))
            {
                _installationMasterDictionary.Remove(installation.Guid);
            }
        }
    }
}