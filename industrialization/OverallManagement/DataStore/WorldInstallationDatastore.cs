using System;
using System.Collections.Generic;
using industrialization.Core.Installation;

namespace industrialization.OverallManagement.DataStore
{
    public static class WorldInstallationDatastore
    {
        //メインのデータストア
        private static Dictionary<Guid, InstallationWorldData> _installationMasterDictionary = new();
        //座標がキーのデータストア
        private static Dictionary<Coordinate,InstallationWorldData> _coordinateDictionary = new();

        public static void AddInstallation(InstallationBase installation,int x,int y)
        {
            if (!_installationMasterDictionary.ContainsKey(installation.Guid))
            {
                var data = new InstallationWorldData(installation, x, y);
                _installationMasterDictionary.Add(installation.Guid,data);
                _coordinateDictionary.Add(new Coordinate {x = x, y = y},data);
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

        public static InstallationBase GetInstallation(int x,int y)
        {
            var c = new Coordinate {x = x, y = y};
            if (_coordinateDictionary.ContainsKey(c))
            {
                return _coordinateDictionary[c].InstallationBase;
            }

            return null;
        }

        public static void RemoveInstallation(InstallationBase installation)
        {
            if (_installationMasterDictionary.ContainsKey(installation.Guid))
            {
                _installationMasterDictionary.Remove(installation.Guid);
                var i = _installationMasterDictionary[installation.Guid];
                _coordinateDictionary.Remove(new Coordinate {x=i.X, y = i.Y});
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

    struct Coordinate
    {
        public int x;
        public int y;
    }
}