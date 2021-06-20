using System;
using System.Collections.Generic;
using industrialization.Core.Installation;

namespace industrialization.OverallManagement.DataStore
{
    public static class WorldInstallationDatastore
    {
        //メインのデータストア
        private static Dictionary<string, InstallationWorldData> _installationMasterDictionary = new();
        //座標がキーのデータストア
        private static Dictionary<Coordinate,InstallationWorldData> _coordinateDictionary = new();


        public static void ClearData()
        {
            _installationMasterDictionary = new();
            _coordinateDictionary = new();
        }
        
        public static bool AddInstallation(InstallationBase installation,int x,int y)
        {
            //既にキーが登録されてないか、同じ座標にブロックを置こうとしてないかをチェック
            if (!_installationMasterDictionary.ContainsKey(installation.Guid.ToString()) &&
                GetInstallation(x,y) == null)
            {
                var data = new InstallationWorldData(installation, x, y);
                _installationMasterDictionary.Add(installation.Guid.ToString(),data);
                _coordinateDictionary.Add(new Coordinate {x = x, y = y},data);

                return true;
            }

            return false;
        }

        public static Coordinate GetCoordinate(Guid guid)
        {
            if (_installationMasterDictionary.ContainsKey(guid.ToString()))
            {
                var i = _installationMasterDictionary[guid.ToString()];
                return new Coordinate {x = i.X, y = i.Y};
            }

            return new Coordinate {x = Int32.MinValue, y = Int32.MinValue};
        }
        
        public static InstallationBase GetInstallation(Guid guid)
        {
            if (_installationMasterDictionary.ContainsKey(guid.ToString()))
            {
                return _installationMasterDictionary[guid.ToString()].InstallationBase;
            }

            return new NullInstallation(-1,Guid.Empty);
        }

        public static InstallationBase GetInstallation(int x,int y)
        {
            var c = new Coordinate {x = x, y = y};
            if (_coordinateDictionary.ContainsKey(c))
            {
                return _coordinateDictionary[c].InstallationBase;
            }

            return new NullInstallation(-1,Guid.Empty);
        }

        public static void RemoveInstallation(InstallationBase installation)
        {
            if (_installationMasterDictionary.ContainsKey(installation.Guid.ToString()))
            {
                _installationMasterDictionary.Remove(installation.Guid.ToString());
                var i = _installationMasterDictionary[installation.Guid.ToString()];
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

    public struct Coordinate
    {
        public int x;
        public int y;
    }
}