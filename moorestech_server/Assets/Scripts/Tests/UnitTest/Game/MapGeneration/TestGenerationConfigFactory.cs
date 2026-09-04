using System.IO;
using Core.Master;
using Mod.Config;
using Mod.Loader;
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
            return GenerationLoader.Load(CreateJsonWithMapObjectGuid(spawnSearchSetup, algorithmParamOverrides, TestMapObjectGuid));
        }

        // 任意のMapObject GUIDを1件だけ持つ生成設定を作り、変換境界の検査に使う。
        // Build generation config with one arbitrary MapObject GUID for conversion-boundary validation.
        public static Generation CreateWithMapObjectGuid(string mapObjectGuid)
        {
            return GenerationLoader.Load(CreateJsonWithMapObjectGuid(SpawnSearchSetup.Enabled, new JObject(), mapObjectGuid));
        }

        // scale差だけのmasterでdrift検証
        // Loads a scale-only master change for drift verification.
        public static void LoadMasterWithMapObjectScaleForProvisioning(float scale)
        {
            var root = CreateJsonWithMapObjectGuid(SpawnSearchSetup.Enabled, new JObject(), TestMapObjectGuid);
            ScatterObjectEntryOf(root)["scaleRange"] = new JArray(scale, scale);
            LoadGenerationMaster(root);
        }

        // 見た目だけが動くmasterでdrift検証。terrainSurroundEffectTypeは配置台帳のdigestに入るが(GUID,座標,scale)集合は動かさない
        // Loads a visuals-only master change for drift verification: terrainSurroundEffectType enters the ledger digest yet leaves the (guid, position, scale) set alone
        public static void LoadMasterWithMapObjectSurroundEffectForProvisioning(string terrainSurroundEffectType)
        {
            var root = CreateJsonWithMapObjectGuid(SpawnSearchSetup.Enabled, new JObject(), TestMapObjectGuid);
            ScatterObjectEntryOf(root)["terrainSurroundEffectType"] = terrainSurroundEffectType;
            LoadGenerationMaster(root);
        }

        private static JObject ScatterObjectEntryOf(JObject root)
        {
            var algorithmParam = (JObject)root["algorithmParam"];
            var entries = (JArray)((JObject)((JObject)algorithmParam["grassland"])["objectConfig"])["entries"];
            return (JObject)entries[0];
        }

        private static void LoadGenerationMaster(JObject root)
        {
            var modResource = new ModsResource(Path.Combine(TestModDirectory.ForUnitTestModDirectory, "mods"));
            var masterContainer = new MasterJsonFileContainer(ModJsonStringLoader.GetMasterString(modResource));
            masterContainer.ConfigJsons[0].JsonContents[new JsonFileName("generation")] =
                root.ToString(Newtonsoft.Json.Formatting.None);
            MasterHolder.Load(masterContainer);
        }

        private static JObject CreateJsonWithMapObjectGuid(
            SpawnSearchSetup spawnSearchSetup,
            JObject algorithmParamOverrides,
            string mapObjectGuid)
        {
            // 鉱脈配置段がveinGuidでmapVeinsマスタを引くため、同じmodのマスタを先にロードする
            // The vein placement stage resolves mapVeins by veinGuid, so load the same mod's masters first
            var modResource = new ModsResource(Path.Combine(TestModDirectory.ForUnitTestModDirectory, "mods"));
            MasterHolder.Load(new MasterJsonFileContainer(ModJsonStringLoader.GetMasterString(modResource)));

            var path = Path.Combine(TestModDirectory.ForUnitTestModDirectory,
                "mods", "forUnitTest", "master", "generation.json");
            var root = JObject.Parse(File.ReadAllText(path));
            var ap = (JObject)root["algorithmParam"];

            // 小さく速い1タイルマップにする（プリセット無視・直接解像度指定）。
            // Make a small, fast single-tile map (bypass preset, set resolution directly).
            ap["overrideResolution"] = spawnSearchSetup == SpawnSearchSetup.Disabled ? 129 : 0;

            // 通常テストはheightmapとdetailを共に縮小し、実生成を1x1へ固定する
            // Shrink both heightmap and detail resolution for ordinary tests and pin real generation to 1x1.
            if (spawnSearchSetup == SpawnSearchSetup.Disabled) ap["detailResolution"] = 128;
            ap["gridSizeX"] = 1;
            ap["gridSizeZ"] = 1;
            // スポーン探索だけはalgorithmParamOverridesから必要な5x5を明示する
            // Spawn-search tests explicitly restore the required 5x5 through algorithmParamOverrides.
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

            return root;

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
                ((JArray)((JObject)((JObject)ap["grassland"])["objectConfig"])["entries"])
                    .Add(TestMapObjectEntryFactory.Create(mapObjectGuid));
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
