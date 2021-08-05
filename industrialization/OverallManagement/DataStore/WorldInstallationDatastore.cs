using System;
using System.Collections.Generic;
using industrialization.Core.Installation;

namespace industrialization.OverallManagement.DataStore
{
    public static class WorldInstallationDatastore
    {
        //メインのデータストア
        private static Dictionary<int, InstallationWorldData> _installationMasterDictionary = new();
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
            if (!ContainsKey(installation.IntId) &&
                !ContainsCoordinate(x,y))
            {
                var data = new InstallationWorldData(installation, x, y);
                _installationMasterDictionary.Add(installation.IntId,data);
                _coordinateDictionary.Add(new Coordinate {x = x, y = y},data);

                return true;
            }

            return false;
        }

        public static bool ContainsKey(int intId)
        {
            return _installationMasterDictionary.ContainsKey(intId);
        }

        public static Coordinate GetCoordinate(int intId)
        {
            if (_installationMasterDictionary.ContainsKey(intId))
            {
                var i = _installationMasterDictionary[intId];
                return new Coordinate {x = i.X, y = i.Y};
            }

            return new Coordinate {x = Int32.MaxValue, y = Int32.MaxValue};
        }
        
        public static InstallationBase GetInstallation(int intId)
        {
            if (_installationMasterDictionary.ContainsKey(intId))
            {
                return _installationMasterDictionary[intId].InstallationBase;
            }

            return new NullInstallation(-1,Int32.MaxValue);
        }

        public static bool ContainsCoordinate(int x, int y)
        {
            var c = new Coordinate {x = x, y = y};
            return _coordinateDictionary.ContainsKey(c);
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
            if (_installationMasterDictionary.ContainsKey(installation.IntId))
            {
                _installationMasterDictionary.Remove(installation.IntId);
                var i = _installationMasterDictionary[installation.IntId];
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