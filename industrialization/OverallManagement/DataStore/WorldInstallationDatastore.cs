using System;
using System.Collections.Generic;
using industrialization.Core.Installation;

namespace industrialization.OverallManagement.DataStore
{
    public static class WorldInstallationDatastore
    {
        private static Dictionary<Guid, InstallationWorldData> _installationMasterDictionary;

        public static void AddInstallation(InstallationBase installation,int x,int y)
        {
            if (!_installationMasterDictionary.ContainsKey(installation.Guid))
            {
                _installationMasterDictionary.Add(installation.Guid,new InstallationWorldData(installation,x,y));
            }
        }

        public static InstallationBase GetInstallation(Guid guid)
        {
            if (_installationMasterDictionary.ContainsKey(guid))
            {
                return _installationMasterDictionary[guid].InstallationBase;
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
        

        class InstallationWorldData
        {
            public InstallationWorldData(InstallationBase installationBase,int x, int y)
            {
                X = x;
                Y = y;
                InstallationBase = installationBase;
            }

            public int X { get; }
            public int Y { get; }
            public InstallationBase InstallationBase { get; }
        
        
        }
    }
}