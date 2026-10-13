using System;
using System.Collections.Generic;
using System.IO;
using Game.Block.Blocks.TrainRail.Naming;
using Game.Block.Interface;
using Game.Map;
using Game.Map.Interface.Json;
using Tests.Module.TestMod;
using UnityEngine;

namespace Tests.UnitTest.Game.TrainStation.Naming
{
    internal static class TrainStationNamingTestInput
    {
        internal const string TreeGuid = "8c0e1339-be75-4690-99cd-58b5385a17cd";
        internal const string TreeTest = "00000000-0000-1111-0000-000000000001";
        internal const string Rock = "00000000-0000-2222-0000-000000000001";
        internal const string Rubble = "00000000-0000-3333-0000-000000000001";
        internal const string Item1 = "item.00000000-0000-0000-1234-000000000001.name";
        internal const string Item2 = "item.00000000-0000-0000-1234-000000000002.name";
        internal const string Fluid1 = "fluid.00000000-0000-0000-1234-000000000001.name";

        internal static MapInfoJson EmptyMap()
        {
            return new MapInfoJson { MapObjects = new List<MapObjectInfoJson>(), MapVeins = new List<MapVeinInfoJson>() };
        }

        internal static void AddObject(MapInfoJson map, string guid)
        {
            map.MapObjects.Add(new MapObjectInfoJson
            {
                InstanceId = map.MapObjects.Count + 1, MapObjectGuidStr = guid, X = 1, Y = 0, Z = 1
            });
        }

        internal static void AddVein(MapInfoJson map, int suffix)
        {
            // 同じ包囲箱で同距離の勝者を識別する
            // Use identical bounds to distinguish the winner of equal distances
            map.MapVeins.Add(new MapVeinInfoJson
            {
                VeinGuidStr = $"11111111-0000-0000-0000-{suffix:D12}",
                MinX = 0, MaxX = 0, MinY = 0, MaxY = 0, MinZ = 0, MaxZ = 0
            });
        }

        internal static string Scan(MapInfoJson map, Dictionary<string, string> names)
        {
            var scanner = new TrainStationNameFeatureScanner(new ItemMapVeinDatastore(map), new FluidMapVeinDatastore(map),
                new MapObjectDatastore(new MapObjectFactory(), map), new TrainStationFeatureEnglishNames(names));
            return scanner.ScanBaseName(new BlockPositionInfo(Vector3Int.zero, BlockDirection.North, new Vector3Int(2, 1, 2)));
        }

        internal static string CopyMod()
        {
            var directory = Path.Combine(Path.GetTempPath(), "station-naming-" + Guid.NewGuid());
            Directory.CreateDirectory(directory);
            // 共有fixtureを変更せず実modの入力経路を使う
            // Exercise real mod input without changing the shared fixture
            foreach (var source in Directory.GetFiles(TestModDirectory.ForUnitTestModDirectory, "*", SearchOption.AllDirectories))
            {
                if (source.EndsWith(".meta", StringComparison.Ordinal)) continue;
                var relative = Path.GetRelativePath(TestModDirectory.ForUnitTestModDirectory, source);
                var target = Path.Combine(directory, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(target));
                File.Copy(source, target);
            }
            return directory;
        }
    }
}
