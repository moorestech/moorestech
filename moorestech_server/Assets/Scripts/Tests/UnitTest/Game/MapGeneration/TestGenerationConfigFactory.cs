using System.IO;
using Mooresmaster.Loader.GenerationModule;
using Mooresmaster.Model.GenerationModule;
using Newtonsoft.Json.Linq;
using Tests.Module.TestMod;

namespace Tests.UnitTest.Game.MapGeneration
{
    // 生成パイプラインのテスト用に小さな Generation を作る（解像度129・1タイル・バイオーム2種・OreEntry1種）。
    // TestMod の generation.json を土台に必要フィールドだけ差し替えて構築する（後続タスクでも使用）。
    // Builds a small Generation for pipeline tests (res 129, 1 tile, 2 biomes, 1 OreEntry).
    // Reuses the TestMod generation.json and overrides only the fields we need.
    public static class TestGenerationConfigFactory
    {
        // OreEntry が参照するテスト用鉱脈 GUID（固定文字列）。
        // Fixed test vein GUID referenced by the OreEntry.
        public const string TestVeinGuid = "11111111-0000-0000-0000-000000000001";

        public static Generation CreateSmall()
        {
            var path = Path.Combine(TestModDirectory.ForUnitTestModDirectory,
                "mods", "forUnitTest", "master", "generation.json");
            var root = JObject.Parse(File.ReadAllText(path));
            var ap = (JObject)root["algorithmParam"];

            // 小さく速い1タイルマップにする（プリセット無視・直接解像度指定）。
            // Make a small, fast single-tile map (bypass preset, set resolution directly).
            ap["overrideResolution"] = 129;
            ap["useSpawnOffsetSearch"] = false;
            ap["generateOre"] = true;

            // 小さな1タイルは低周波の大陸性ノイズだと全面が海になりうるため、閾値を下げて陸を保証する。
            // A small single tile can turn all-ocean under low-frequency continentalness; lower the threshold to guarantee land.
            ap["landThreshold"] = 0.0;

            // バイオームは Grassland + Forest の2種に絞る。
            // Restrict biomes to Grassland + Forest only.
            ap["grasslandEnabled"] = true;
            ap["forestEnabled"] = true;
            ap["savannaEnabled"] = false;
            ap["desertEnabled"] = false;
            ap["mesaEnabled"] = false;
            ap["alpineEnabled"] = false;
            ap["jungleEnabled"] = false;
            ap["woodsEnabled"] = false;

            // OreEntry を有効なバンド1本＋固定 GUID＋Grassland 出現に設定して鉱脈が必ず生成されるようにする。
            // Configure the OreEntry with one valid band, fixed GUID, Grassland, so veins are always produced.
            var ore = (JObject)ap["oreConfig"];
            var entries = (JArray)ore["entries"];
            var entry0 = (JObject)entries[0];
            entry0["veinGuid"] = TestVeinGuid;
            entry0["biomes"] = new JArray("Grassland", "Forest");
            entry0["useSlopeFilter"] = false;
            entry0["minDistanceFromOthers"] = 0;
            entry0["bands"] = new JArray(new JObject
            {
                ["outerRadiusMeters"] = -1,
                ["density"] = 1.0,
                ["maxObjectsPerCluster"] = 5,
                ["clusterRadius"] = 6,
                ["minDistanceBetweenOres"] = 1,
                ["placementRetries"] = 10,
            });

            return GenerationLoader.Load(root);
        }
    }
}
