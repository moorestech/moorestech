using System.Diagnostics;
using System.Text;
using MapGenerator.Pipeline;
using MapGenerator.Pipeline.Config;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace MapGenerator.Editor
{
    /// <summary>
    /// パイプライン各ステージの所要時間を計測するエディタツール。
    /// シーン上の MapGeneratorFacade から config を取得し、指定条件で生成→Apply の全工程を計測する。
    /// </summary>
    public static class PipelinePerformanceMeasurement
    {
        [MenuItem("Tools/MapGenerator/Measure Pipeline Performance")]
        public static void Measure()
        {
            // シーン上から config を取得（InfiniteTerrainManager → MapGeneratorFacade の順で探索）
            TerrainGenerationConfig config = null;
            Terrain[] sceneTerrains = null;

            var itmGo = GameObject.Find("InfiniteTerrainManager");
            if (itmGo != null)
            {
                var itm = itmGo.GetComponent<InfiniteTerrainManager>();
                if (itm != null)
                {
                    config = itm.baseConfig;
                    sceneTerrains = itmGo.GetComponentsInChildren<Terrain>();
                }
            }

            if (config == null)
            {
                var facadeGo = GameObject.Find("MapGenerator");
                if (facadeGo != null)
                {
                    var facade = facadeGo.GetComponent<MapGeneratorFacade>();
                    if (facade != null)
                    {
                        config = facade.config;
                        sceneTerrains = facade.CollectTerrains();
                    }
                }
            }

            if (config == null)
            {
                Debug.LogError("[PerfMeasure] TerrainGenerationConfig が見つかりません");
                return;
            }

            // 計測条件を一時的に上書き（終了後に復元）
            var origRes = config.resolutionPreset;
            var origGridX = config.gridSizeX;
            var origGridZ = config.gridSizeZ;
            var origHeight = config.generateHeightmap;
            var origTex = config.generateTexture;
            var origDetail = config.generateDetail;
            var origObj = config.generateObject;
            var origOre = config.generateOre;
            var origGrassland = config.grasslandEnabled;
            var origForest = config.forestEnabled;
            var origSavanna = config.savannaEnabled;
            var origDesert = config.desertEnabled;
            var origMesa = config.mesaEnabled;
            var origAlpine = config.alpineEnabled;
            var origJungle = config.jungleEnabled;
            var origWoods = config.woodsEnabled;

            try
            {
                // 計測条件: 2048x2048, 全バイオームON, 全項目ON, 10x10タイル
                config.resolutionPreset = TerrainResolutionPreset._2048;
                config.gridSizeX = 10;
                config.gridSizeZ = 10;
                config.generateHeightmap = true;
                config.generateTexture = true;
                config.generateDetail = true;
                config.generateObject = true;
                config.generateOre = true;
                config.grasslandEnabled = true;
                config.forestEnabled = true;
                config.savannaEnabled = true;
                config.desertEnabled = true;
                config.mesaEnabled = true;
                config.alpineEnabled = true;
                config.jungleEnabled = true;
                config.woodsEnabled = true;

                int res = config.Resolution;
                var sb = new StringBuilder();
                sb.AppendLine("============================================================");
                sb.AppendLine("  パイプラインパフォーマンス計測");
                sb.AppendLine("============================================================");
                sb.AppendLine($"  解像度: {res}x{res}");
                sb.AppendLine($"  タイル: {config.gridSizeX}x{config.gridSizeZ}");
                sb.AppendLine($"  バイオーム: 全8種ON");
                sb.AppendLine($"  生成項目: Height/Texture/Detail/Object/Ore 全ON");
                sb.AppendLine("------------------------------------------------------------");

                // --- Phase 1: Generate ---
                PipelineProfiler.Enabled = true;
                var swTotal = Stopwatch.StartNew();
                var result = TerrainGenerator.Generate(config);
                long generateMs = swTotal.ElapsedMilliseconds;
                PipelineProfiler.Enabled = false;

                sb.AppendLine();
                sb.AppendLine($"【Generate フェーズ】合計: {generateMs}ms");
                sb.AppendLine(PipelineProfiler.Report());

                // --- Phase 2: ApplyToGrid ---
                sb.AppendLine("------------------------------------------------------------");
                if (sceneTerrains != null && sceneTerrains.Length > 0)
                {
                    var swApply = Stopwatch.StartNew();
                    TerrainApplier.ApplyToGrid(sceneTerrains, result);
                    long applyMs = swApply.ElapsedMilliseconds;
                    sb.AppendLine($"【ApplyToGrid フェーズ】{sceneTerrains.Length} タイル: {applyMs}ms");
                }
                else
                {
                    sb.AppendLine("【ApplyToGrid フェーズ】テレインなし（スキップ）");
                }

                // --- サマリ ---
                sb.AppendLine();
                sb.AppendLine("============================================================");
                sb.AppendLine($"  総合計: {swTotal.ElapsedMilliseconds}ms");
                sb.AppendLine("============================================================");

                // 結果の概要情報
                sb.AppendLine();
                sb.AppendLine("[生成結果サマリ]");
                sb.AppendLine($"  Heights: {(result.Heights != null ? result.Heights.Length.ToString("N0") : "null")} pixels");
                if (result.Splatmap != null)
                    sb.AppendLine($"  Splatmap: {result.Splatmap.GetLength(0)}x{result.Splatmap.GetLength(1)}x{result.Splatmap.GetLength(2)}");
                sb.AppendLine($"  Trees: {result.TreeInstances?.Length ?? 0}");
                sb.AppendLine($"  Objects: {result.ObjectPlacements?.Count ?? 0}");
                sb.AppendLine($"  Details: {result.DetailMaps?.Count ?? 0} layers");
                sb.AppendLine($"  Ores: {result.OrePlacements?.Count ?? 0}");

                Debug.Log(sb.ToString());
            }
            finally
            {
                // 設定を確実に復元
                config.resolutionPreset = origRes;
                config.gridSizeX = origGridX;
                config.gridSizeZ = origGridZ;
                config.generateHeightmap = origHeight;
                config.generateTexture = origTex;
                config.generateDetail = origDetail;
                config.generateObject = origObj;
                config.generateOre = origOre;
                config.grasslandEnabled = origGrassland;
                config.forestEnabled = origForest;
                config.savannaEnabled = origSavanna;
                config.desertEnabled = origDesert;
                config.mesaEnabled = origMesa;
                config.alpineEnabled = origAlpine;
                config.jungleEnabled = origJungle;
                config.woodsEnabled = origWoods;
            }
        }
    }
}
