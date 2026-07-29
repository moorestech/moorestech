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

        // FluidVeinEntry が参照するテスト用鉱脈 GUID（map.json の test:WaterVein）。
        // Fixed test vein GUID referenced by the FluidVeinEntry (test:WaterVein in map.json).
        public const string TestFluidVeinGuid = "11111111-0000-0000-0000-000000000002";

        // ObjectEntry が参照するテスト用マップオブジェクト GUID（map.json の vanilla:Tree）。
        // Fixed test map object GUID referenced by the ObjectEntry (vanilla:Tree in map.json).
        public const string TestMapObjectGuid = "8c0e1339-be75-4690-99cd-58b5385a17cd";

        // スポーン探索の有無を選ぶ。探索有効時は本番解像度が必須（段2検証が overrideResolution を拒否する）。
        // Selects the spawn-search setup; enabling it requires the production resolution (stage 2 rejects overrideResolution).
        public enum SpawnSearchSetup
        {
            Disabled,
            Enabled,
            Unsatisfiable,
        }

        public static Generation CreateSmall()
        {
            return Create(SpawnSearchSetup.Disabled);
        }

        public static Generation Create(SpawnSearchSetup spawnSearchSetup)
        {
            var path = Path.Combine(TestModDirectory.ForUnitTestModDirectory,
                "mods", "forUnitTest", "master", "generation.json");
            var root = JObject.Parse(File.ReadAllText(path));
            var ap = (JObject)root["algorithmParam"];

            // 小さく速い1タイルマップにする（プリセット無視・直接解像度指定）。
            // Make a small, fast single-tile map (bypass preset, set resolution directly).
            ap["overrideResolution"] = spawnSearchSetup == SpawnSearchSetup.Disabled ? 129 : 0;
            ap["useSpawnOffsetSearch"] = spawnSearchSetup != SpawnSearchSetup.Disabled;
            ap["generateOre"] = true;
            ConfigureSpawnSearch((JObject)ap["spawnSearch"], spawnSearchSetup);

            // 小さな1タイルは低周波の大陸性ノイズだと全面が海になりうるため、閾値を下げて陸を保証する。
            // A small single tile can turn all-ocean under low-frequency continentalness; lower the threshold to guarantee land.
            ap["landThreshold"] = 0.0;

            // ブレンド半径はスポーン探索の縁マージン(≒blendRadius×m/px)を決めるため、狭い検証窓でも中心が残る値にする。
            // The blend radius drives the spawn-search edge margin (~blendRadius x m/px), so keep it small enough for a narrow window.
            ap["biomeBlendRadius"] = 20;

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

            // OreEntry/FluidVeinEntry を有効なバンド1本＋固定 GUID＋Grassland 出現に設定して
            // 鉱脈が必ず生成されるようにする（item/fluidとも同形）。
            // Configure OreEntry/FluidVeinEntry with one valid band, fixed GUID, Grassland, so veins are
            // always produced (item/fluid share the same shape).
            var ore = (JObject)ap["oreConfig"];
            ConfigureVeinEntry((JObject)((JArray)ore["entries"])[0], TestVeinGuid);
            ConfigureVeinEntry((JObject)((JArray)ore["fluidEntries"])[0], TestFluidVeinGuid);

            // 独立散布オブジェクトを Grassland に1種置き、MapObjects が空にならないようにする。
            // Place one independently scattered object in Grassland so MapObjects is never empty.
            ((JArray)((JObject)((JObject)ap["grassland"])["objectConfig"])["entries"]).Add(BuildObjectEntry());

            return GenerationLoader.Load(root);

            #region Internal

            // 探索コストを抑えつつ確実に成功させる。Unsatisfiable は面積条件を満たせない値にして必ずフォールバックさせる。
            // Keep the search cheap yet reliably successful; Unsatisfiable uses an unreachable area so it always falls back.
            static void ConfigureSpawnSearch(JObject spawnSearch, SpawnSearchSetup setup)
            {
                if (setup == SpawnSearchSetup.Disabled) return;

                spawnSearch["maxDetailedResolution"] = 512;
                spawnSearch["topK"] = 4;
                spawnSearch["maxExpandIterations"] = 1;
                if (setup == SpawnSearchSetup.Unsatisfiable)
                    spawnSearch["minGrasslandArea"] = 1e12;
            }

            // ノイズ・傘フィルタを全て無効にした素の散布エントリ。スキーマ既定値と同値でも明示的に埋める。
            // A bare scatter entry with every noise/slope filter off; fields are written out even when equal to the schema defaults.
            static JObject BuildObjectEntry()
            {
                return new JObject
                {
                    ["prefabs"] = new JArray(new JObject { ["mapObjectGuid"] = TestMapObjectGuid }),
                    ["density"] = 1.0,
                    ["scaleRange"] = new JArray(1.0, 1.0),
                    ["slopeAlignment"] = 0.0,
                    ["sinkRange"] = new JArray(0.0, 0.0),
                    ["noiseType"] = "None",
                    ["noiseFrequency"] = 10.0,
                    ["noiseAmplitude"] = 1.0,
                    ["noiseThreshold"] = 0.5,
                    ["useSlopeFilter"] = false,
                    ["slopeMin"] = 0.0,
                    ["slopeMax"] = 90.0,
                    ["slopeSmoothness"] = 4.0,
                    ["useClusterMode"] = false,
                    ["clusterCount"] = 8,
                    ["objectsPerCluster"] = 4,
                    ["clusterRadius"] = 12.0,
                    ["minDistanceFromTree"] = 0.0,
                    ["maxDistanceFromTree"] = 0.0,
                };
            }

            static void ConfigureVeinEntry(JObject entry, string veinGuid)
            {
                entry["veinGuid"] = veinGuid;
                entry["biomes"] = new JArray("Grassland", "Forest");
                entry["useSlopeFilter"] = false;
                entry["minDistanceFromOthers"] = 0;
                entry["bands"] = new JArray(new JObject
                {
                    ["outerRadiusMeters"] = -1,
                    ["density"] = 1.0,
                    ["maxObjectsPerCluster"] = 5,
                    ["clusterRadius"] = 6,
                    ["minDistanceBetweenOres"] = 1,
                    ["placementRetries"] = 10,
                });
            }

            #endregion
        }
    }
}
