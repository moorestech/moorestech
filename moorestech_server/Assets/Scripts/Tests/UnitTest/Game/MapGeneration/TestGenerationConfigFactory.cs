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
        // MapGenerationPipeline.Generate が texturePngPath を解決する基準。TestMod は PNG を持たない。
        // Base directory MapGenerationPipeline.Generate resolves texturePngPath against; TestMod ships no PNG.
        public static string ServerDataDirectory => TestModDirectory.ForUnitTestModDirectory;

        // OreEntry が参照するテスト用鉱脈 GUID（固定文字列）。
        // Fixed test vein GUID referenced by the OreEntry.
        public const string TestVeinGuid = "11111111-0000-0000-0000-000000000001";

        // FluidVeinEntry が参照するテスト用鉱脈 GUID（map.json の test:WaterVein）。
        // Fixed test vein GUID referenced by the FluidVeinEntry (test:WaterVein in map.json).
        public const string TestFluidVeinGuid = "11111111-0000-0000-0000-000000000002";

        // ObjectEntry が参照するテスト用マップオブジェクト GUID（map.json の vanilla:Tree）。
        // Fixed test map object GUID referenced by the ObjectEntry (vanilla:Tree in map.json).
        public const string TestMapObjectGuid = "8c0e1339-be75-4690-99cd-58b5385a17cd";

        // 鉱脈が格子外へはみ出しうる量。ADR-0023 の仕様値で、実装定数を読まず独立に持つ。
        // The overhang a vein may have past the grid: the ADR-0023 spec value, held independently of the implementation constant.
        public const int VeinGridOverhang = 1;

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
            return CreateWithAlgorithmParamOverrides(spawnSearchSetup, new JObject());
        }

        // algorithmParam の任意フィールドを差し替えて構築する。座標系まわりの条件を1件だけ変えたいテスト用。
        // Builds with arbitrary algorithmParam fields replaced, for tests varying a single coordinate-system condition.
        public static Generation CreateWithAlgorithmParamOverrides(SpawnSearchSetup spawnSearchSetup, JObject algorithmParamOverrides)
        {
            return CreateWithMapObjectGuid(spawnSearchSetup, algorithmParamOverrides, TestMapObjectGuid);
        }

        // 任意のMapObject GUIDを1件だけ持つ生成設定を作り、変換境界の検査に使う。
        // Build generation config with one arbitrary MapObject GUID for conversion-boundary validation.
        public static Generation CreateWithMapObjectGuid(string mapObjectGuid)
        {
            return CreateWithMapObjectGuid(SpawnSearchSetup.Enabled, new JObject(), mapObjectGuid);
        }

        private static Generation CreateWithMapObjectGuid(
            SpawnSearchSetup spawnSearchSetup,
            JObject algorithmParamOverrides,
            string mapObjectGuid)
        {
            var path = Path.Combine(TestModDirectory.ForUnitTestModDirectory,
                "mods", "forUnitTest", "master", "generation.json");
            var root = JObject.Parse(File.ReadAllText(path));
            var ap = (JObject)root["algorithmParam"];

            // 小さく速い1タイルマップにする（プリセット無視・直接解像度指定）。
            // Make a small, fast single-tile map (bypass preset, set resolution directly).
            ap["overrideResolution"] = spawnSearchSetup == SpawnSearchSetup.Disabled ? 129 : 0;

            // forUnitTest の generation.json は 5x5 なので、多タイルを要らないテストのために 1x1 へ落とす。
            // 5x5 を要るテスト（スポーン探索系）は algorithmParamOverrides で明示的に戻すこと。
            // The forUnitTest generation.json ships 5x5, so drop to 1x1 for tests that do not need multiple tiles.
            // Tests that do need 5x5 (the spawn-search ones) restore it explicitly through algorithmParamOverrides.
            ap["gridSizeX"] = 1;
            ap["gridSizeZ"] = 1;

            ap["useSpawnOffsetSearch"] = spawnSearchSetup != SpawnSearchSetup.Disabled;
            ap["generateOre"] = true;
            ConfigureSpawnSearch((JObject)ap["spawnSearch"], spawnSearchSetup);

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

            // OreEntry/FluidVeinEntry を有効なバンド1本＋固定 GUID＋Grassland 出現に設定して
            // 鉱脈が必ず生成されるようにする（item/fluidとも同形）。
            // Configure OreEntry/FluidVeinEntry with one valid band, fixed GUID, Grassland, so veins are
            // always produced (item/fluid share the same shape).
            var ore = (JObject)ap["oreConfig"];
            ConfigureVeinEntry((JObject)((JArray)ore["entries"])[0], TestVeinGuid);
            ConfigureVeinEntry((JObject)((JArray)ore["fluidEntries"])[0], TestFluidVeinGuid);

            ConfigureForSpawnSearch(ap, spawnSearchSetup, mapObjectGuid);

            // 差し替えは最後に当てる。setup 側の既定を上書きしたいテストが必ず勝つようにするため。
            // Overrides land last so a test that wants to replace a setup default always wins.
            foreach (var overrideProperty in algorithmParamOverrides.Properties())
                ap[overrideProperty.Name] = overrideProperty.Value;

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

            // 探索経路だけに要る調整。Disabled を使う既存テストの地形・配置物を動かさないよう分岐の内側に置く。
            // Tuning needed only by the search path; kept inside the branch so Disabled tests keep their terrain and placements.
            static void ConfigureForSpawnSearch(JObject ap, SpawnSearchSetup setup, string mapObjectGuid)
            {
                if (setup == SpawnSearchSetup.Disabled) return;

                // ブレンド半径はスポーン探索の縁マージン(≒blendRadius×m/px)を決めるため、狭い検証窓でも中心が残る値にする。
                // The blend radius drives the spawn-search edge margin (~blendRadius x m/px), so keep it small enough for a narrow window.
                ap["biomeBlendRadius"] = 20;

                // 独立散布オブジェクトを Grassland に1種置き、MapObjects が空にならないようにする。
                // Place one independently scattered object in Grassland so MapObjects is never empty.
                ((JArray)((JObject)((JObject)ap["grassland"])["objectConfig"])["entries"]).Add(BuildObjectEntry(mapObjectGuid));
            }

            // ノイズ・傘フィルタを全て無効にした素の散布エントリ。スキーマ既定値と同値でも明示的に埋める。
            // A bare scatter entry with every noise/slope filter off; fields are written out even when equal to the schema defaults.
            static JObject BuildObjectEntry(string mapObjectGuid)
            {
                return new JObject
                {
                    ["prefabs"] = new JArray(new JObject { ["mapObjectGuid"] = mapObjectGuid }),
                    // 外半径・densityが互いに違う2帯にして、帯とリングの対応が入れ替わる改変を転写テストで捕まえる
                    // Two bands differing in both radius and density, so a mix-up between bands and rings fails the transcription test
                    ["placementMode"] = "scatter",
                    ["placementParam"] = new JObject
                    {
                        ["bands"] = new JArray(
                            new JObject
                            {
                                ["outerRadiusMeters"] = 250.0,
                                ["pointsPerHectare"] = 2.0,
                            },
                            new JObject
                            {
                                ["outerRadiusMeters"] = -1,
                                ["pointsPerHectare"] = 1.0,
                            }),
                    },
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
                    ["minDistanceBetweenOres"] = 4,
                    ["placementRetries"] = 10,
                });
            }

            #endregion
        }
    }
}
