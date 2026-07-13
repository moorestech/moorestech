using System.IO;
using MapGenerator.Pipeline;
using MapGenerator.Pipeline.Biomes;
using MapGenerator.Pipeline.Config;
using MapGenerator.Pipeline.Generators.Util;
using UnityEditor;
using UnityEngine;

namespace MapGenerator.Editor
{
    /// <summary>
    /// SDF距離マップの現行アルゴリズム出力をバイナリファイルとして保存する。
    /// EDT実装後に同条件で再生成し、保存データと全ピクセル一致を検証する。
    /// </summary>
    public static class SdfReferenceExporter
    {
        const string OutputDir = "Assets/MapGenerator/Tests/EditMode/SdfReference";

        [MenuItem("Tools/MapGenerator/Export SDF Reference Data")]
        public static void Export()
        {
            // シーンから config を取得
            var config = FindConfig();
            if (config == null)
            {
                Debug.LogError("[SdfRef] config が見つかりません");
                return;
            }

            // 計測条件を固定: 2048, 全バイオーム, 全項目ON
            var origRes = config.resolutionPreset;
            var origGrassland = config.grasslandEnabled;
            var origForest = config.forestEnabled;
            var origSavanna = config.savannaEnabled;
            var origDesert = config.desertEnabled;
            var origMesa = config.mesaEnabled;
            var origAlpine = config.alpineEnabled;
            var origJungle = config.jungleEnabled;
            var origWoods = config.woodsEnabled;
            var origObj = config.generateObject;
            var origDetail = config.generateDetail;
            var origOre = config.generateOre;

            try
            {
                config.resolutionPreset = TerrainResolutionPreset._2048;
                config.grasslandEnabled = true;
                config.forestEnabled = true;
                config.savannaEnabled = true;
                config.desertEnabled = true;
                config.mesaEnabled = true;
                config.alpineEnabled = true;
                config.jungleEnabled = true;
                config.woodsEnabled = true;
                config.generateObject = true;
                config.generateDetail = true;
                config.generateOre = true;

                // パイプラインを途中まで実行してSDF入力データを構築
                var helper = new BiomePlacementHelper(config);
                var biomeTypes = GetEnabledBiomeTypes(config);
                int res = config.Resolution;

                // 全パイプラインを実行（Tree/Object配置結果が必要）
                var result = TerrainGenerator.Generate(config);

                // Tree/Object の SpatialGrid を再構築
                var treeSpatialGrid = BuildTreeSpatialGrid(result, config);
                var objectSpatialGrid = BuildObjectSpatialGrid(result, config);

                // 出力ディレクトリ作成
                Directory.CreateDirectory(OutputDir);

                int alphaRes = config.AlphamapResolution;
                int savedCount = 0;

                // メタデータ保存
                var metaPath = Path.Combine(OutputDir, "meta.txt");
                using (var sw = new StreamWriter(metaPath))
                {
                    sw.WriteLine($"resolution={res}");
                    sw.WriteLine($"alphamapResolution={alphaRes}");
                    sw.WriteLine($"terrainWidth={config.terrainWidth}");
                    sw.WriteLine($"terrainLength={config.terrainLength}");
                    sw.WriteLine($"treeCount={result.TreeInstances?.Length ?? 0}");
                    sw.WriteLine($"objectCount={result.ObjectPlacements?.Count ?? 0}");
                }

                for (int b = 0; b < biomeTypes.Length; b++)
                {
                    var dc = helper.GetDetailConfig(biomeTypes[b]);
                    if (dc?.entries == null || dc.entries.Length == 0) continue;

                    float treeMaxR = SdfMapGenerator.ComputeMaxSearchRadius(dc.entries, true);
                    float objMaxR = SdfMapGenerator.ComputeMaxSearchRadius(dc.entries, false);

                    if (treeMaxR > 0f)
                    {
                        var treeDistMap = SdfMapGenerator.Generate(
                            treeSpatialGrid, alphaRes, config.terrainWidth, config.terrainLength, treeMaxR);
                        SaveFloatMap(treeDistMap, Path.Combine(OutputDir, $"tree_{biomeTypes[b]}.bin"));
                        Debug.Log($"[SdfRef] Saved tree_{biomeTypes[b]}.bin ({alphaRes}x{alphaRes}, maxR={treeMaxR:F2})");
                        savedCount++;
                    }

                    if (objMaxR > 0f)
                    {
                        var objDistMap = SdfMapGenerator.Generate(
                            objectSpatialGrid, alphaRes, config.terrainWidth, config.terrainLength, objMaxR);
                        SaveFloatMap(objDistMap, Path.Combine(OutputDir, $"obj_{biomeTypes[b]}.bin"));
                        Debug.Log($"[SdfRef] Saved obj_{biomeTypes[b]}.bin ({alphaRes}x{alphaRes}, maxR={objMaxR:F2})");
                        savedCount++;
                    }
                }

                Debug.Log($"[SdfRef] {savedCount} 個のリファレンスファイルを {OutputDir} に保存しました");
                AssetDatabase.Refresh();
            }
            finally
            {
                config.resolutionPreset = origRes;
                config.grasslandEnabled = origGrassland;
                config.forestEnabled = origForest;
                config.savannaEnabled = origSavanna;
                config.desertEnabled = origDesert;
                config.mesaEnabled = origMesa;
                config.alpineEnabled = origAlpine;
                config.jungleEnabled = origJungle;
                config.woodsEnabled = origWoods;
                config.generateObject = origObj;
                config.generateDetail = origDetail;
                config.generateOre = origOre;
            }
        }

        /// <summary>
        /// EDT実装後にリファレンスデータと全ピクセル一致を検証する。
        /// </summary>
        [MenuItem("Tools/MapGenerator/Verify SDF Against Reference")]
        public static void Verify()
        {
            var config = FindConfig();
            if (config == null)
            {
                Debug.LogError("[SdfRef] config が見つかりません");
                return;
            }

            if (!Directory.Exists(OutputDir))
            {
                Debug.LogError("[SdfRef] リファレンスデータが存在しません。先に Export を実行してください");
                return;
            }

            var origRes = config.resolutionPreset;
            var origGrassland = config.grasslandEnabled;
            var origForest = config.forestEnabled;
            var origSavanna = config.savannaEnabled;
            var origDesert = config.desertEnabled;
            var origMesa = config.mesaEnabled;
            var origAlpine = config.alpineEnabled;
            var origJungle = config.jungleEnabled;
            var origWoods = config.woodsEnabled;
            var origObj = config.generateObject;
            var origDetail = config.generateDetail;
            var origOre = config.generateOre;

            try
            {
                config.resolutionPreset = TerrainResolutionPreset._2048;
                config.grasslandEnabled = true;
                config.forestEnabled = true;
                config.savannaEnabled = true;
                config.desertEnabled = true;
                config.mesaEnabled = true;
                config.alpineEnabled = true;
                config.jungleEnabled = true;
                config.woodsEnabled = true;
                config.generateObject = true;
                config.generateDetail = true;
                config.generateOre = true;

                var helper = new BiomePlacementHelper(config);
                var biomeTypes = GetEnabledBiomeTypes(config);
                var result = TerrainGenerator.Generate(config);
                var treeSpatialGrid = BuildTreeSpatialGrid(result, config);
                var objectSpatialGrid = BuildObjectSpatialGrid(result, config);

                int alphaRes = config.AlphamapResolution;
                int totalFiles = 0;
                int passedFiles = 0;

                for (int b = 0; b < biomeTypes.Length; b++)
                {
                    var dc = helper.GetDetailConfig(biomeTypes[b]);
                    if (dc?.entries == null || dc.entries.Length == 0) continue;

                    float treeMaxR = SdfMapGenerator.ComputeMaxSearchRadius(dc.entries, true);
                    float objMaxR = SdfMapGenerator.ComputeMaxSearchRadius(dc.entries, false);

                    if (treeMaxR > 0f)
                    {
                        var treeDistMap = SdfMapGenerator.Generate(
                            treeSpatialGrid, alphaRes, config.terrainWidth, config.terrainLength, treeMaxR);
                        var refPath = Path.Combine(OutputDir, $"tree_{biomeTypes[b]}.bin");
                        totalFiles++;
                        if (CompareWithReference(treeDistMap, refPath, $"tree_{biomeTypes[b]}"))
                            passedFiles++;
                    }

                    if (objMaxR > 0f)
                    {
                        var objDistMap = SdfMapGenerator.Generate(
                            objectSpatialGrid, alphaRes, config.terrainWidth, config.terrainLength, objMaxR);
                        var refPath = Path.Combine(OutputDir, $"obj_{biomeTypes[b]}.bin");
                        totalFiles++;
                        if (CompareWithReference(objDistMap, refPath, $"obj_{biomeTypes[b]}"))
                            passedFiles++;
                    }
                }

                if (passedFiles == totalFiles)
                    Debug.Log($"[SdfRef] ✅ 全 {totalFiles} ファイルが完全一致");
                else
                    Debug.LogError($"[SdfRef] ❌ {totalFiles - passedFiles}/{totalFiles} ファイルで不一致");
            }
            finally
            {
                config.resolutionPreset = origRes;
                config.grasslandEnabled = origGrassland;
                config.forestEnabled = origForest;
                config.savannaEnabled = origSavanna;
                config.desertEnabled = origDesert;
                config.mesaEnabled = origMesa;
                config.alpineEnabled = origAlpine;
                config.jungleEnabled = origJungle;
                config.woodsEnabled = origWoods;
                config.generateObject = origObj;
                config.generateDetail = origDetail;
                config.generateOre = origOre;
            }
        }

        static bool CompareWithReference(float[,] current, string refPath, string label)
        {
            if (!File.Exists(refPath))
            {
                Debug.LogError($"[SdfRef] {label}: リファレンスファイルが存在しません: {refPath}");
                return false;
            }

            var reference = LoadFloatMap(refPath);
            int h = current.GetLength(0);
            int w = current.GetLength(1);

            if (reference.GetLength(0) != h || reference.GetLength(1) != w)
            {
                Debug.LogError($"[SdfRef] {label}: サイズ不一致 current={h}x{w} ref={reference.GetLength(0)}x{reference.GetLength(1)}");
                return false;
            }

            // 全ピクセル比較（許容誤差 1e-4m = 0.1mm）
            float maxDiff = 0f;
            int mismatchCount = 0;
            int firstMismatchX = -1, firstMismatchZ = -1;
            float firstMismatchRef = 0f, firstMismatchCur = 0f;

            for (int z = 0; z < h; z++)
            {
                for (int x = 0; x < w; x++)
                {
                    float diff = Mathf.Abs(current[z, x] - reference[z, x]);
                    if (diff > maxDiff) maxDiff = diff;
                    if (diff > 1e-4f)
                    {
                        if (mismatchCount == 0)
                        {
                            firstMismatchX = x;
                            firstMismatchZ = z;
                            firstMismatchRef = reference[z, x];
                            firstMismatchCur = current[z, x];
                        }
                        mismatchCount++;
                    }
                }
            }

            if (mismatchCount > 0)
            {
                Debug.LogError($"[SdfRef] {label}: ❌ {mismatchCount}/{h * w} ピクセル不一致 " +
                               $"(最大差={maxDiff:F6}m, 最初の不一致=[{firstMismatchZ},{firstMismatchX}] " +
                               $"ref={firstMismatchRef:F6} cur={firstMismatchCur:F6})");
                return false;
            }

            Debug.Log($"[SdfRef] {label}: ✅ 全ピクセル一致 (最大差={maxDiff:E2}m)");
            return true;
        }

        static void SaveFloatMap(float[,] map, string path)
        {
            int h = map.GetLength(0);
            int w = map.GetLength(1);
            using var fs = new FileStream(path, FileMode.Create);
            using var bw = new BinaryWriter(fs);
            bw.Write(h);
            bw.Write(w);
            for (int z = 0; z < h; z++)
                for (int x = 0; x < w; x++)
                    bw.Write(map[z, x]);
        }

        static float[,] LoadFloatMap(string path)
        {
            using var fs = new FileStream(path, FileMode.Open);
            using var br = new BinaryReader(fs);
            int h = br.ReadInt32();
            int w = br.ReadInt32();
            var map = new float[h, w];
            for (int z = 0; z < h; z++)
                for (int x = 0; x < w; x++)
                    map[z, x] = br.ReadSingle();
            return map;
        }

        static TerrainGenerationConfig FindConfig()
        {
            var itmGo = GameObject.Find("InfiniteTerrainManager");
            if (itmGo != null)
            {
                var itm = itmGo.GetComponent<InfiniteTerrainManager>();
                if (itm?.baseConfig != null) return itm.baseConfig;
            }
            var facadeGo = GameObject.Find("MapGenerator");
            if (facadeGo != null)
            {
                var facade = facadeGo.GetComponent<MapGeneratorFacade>();
                if (facade?.config != null) return facade.config;
            }
            return null;
        }

        // TerrainGenerator の private メソッドと同等のロジック
        static BiomeType[] GetEnabledBiomeTypes(TerrainGenerationConfig config)
        {
            var list = new System.Collections.Generic.List<BiomeType>();
            if (config.alpineEnabled)    list.Add(BiomeType.Alpine);
            if (config.mesaEnabled)      list.Add(BiomeType.Mesa);
            if (config.jungleEnabled)    list.Add(BiomeType.Jungle);
            if (config.desertEnabled)    list.Add(BiomeType.Desert);
            if (config.forestEnabled)    list.Add(BiomeType.Forest);
            if (config.woodsEnabled)     list.Add(BiomeType.Woods);
            if (config.savannaEnabled)   list.Add(BiomeType.Savanna);
            if (config.grasslandEnabled) list.Add(BiomeType.Grassland);
            if (list.Count == 0) list.Add(BiomeType.Grassland);
            return list.ToArray();
        }

        static SpatialGrid BuildTreeSpatialGrid(TerrainGenerationResult result, TerrainGenerationConfig config)
        {
            float cellSize = Mathf.Max(config.terrainWidth / 50f, 5f);
            var grid = new SpatialGrid(config.terrainWidth, config.terrainLength, cellSize);
            if (result.TreeInstances == null) return grid;
            foreach (var t in result.TreeInstances)
            {
                float wx = t.position.x * config.terrainWidth;
                float wz = t.position.z * config.terrainLength;
                grid.Add(wx, wz);
            }
            return grid;
        }

        static SpatialGrid BuildObjectSpatialGrid(TerrainGenerationResult result, TerrainGenerationConfig config)
        {
            float cellSize = Mathf.Max(config.terrainWidth / 50f, 5f);
            var grid = new SpatialGrid(config.terrainWidth, config.terrainLength, cellSize);
            if (result.ObjectPlacements == null) return grid;
            foreach (var p in result.ObjectPlacements)
            {
                grid.Add(p.Position.x - config.worldOffsetX, p.Position.z - config.worldOffsetZ);
            }
            return grid;
        }
    }
}
