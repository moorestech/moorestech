using System.Collections.Generic;
using System.IO;
using MapGenerator.Pipeline;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace MapGenerator.EditorExport
{
    // DefaultConfig(SO)を読み、generation.yml スキーマ準拠の generation.json を v8 mod に書き出す。
    // Reads DefaultConfig SO and writes a generation.yml-conformant generation.json into the v8 mod.
    public static class GenerationConfigExporter
    {
        private const string ConfigPath = "Assets/MapGenerator/Presets/DefaultConfig.asset";
        private const string MapJsonPath = "/Users/katsumi/moorestech_master/server_v8/mods/moorestechAlphaMod_8/master/map.json";
        private const string OutputPath = "/Users/katsumi/moorestech_master/server_v8/mods/moorestechAlphaMod_8/master/generation.json";

        [MenuItem("Tools/MapGenerator/Export Generation Config")]
        public static void Export()
        {
            // 生成器選択と優先度はユーザー裁定済みの v8 実データ値
            // Algorithm selection and priority are the user-arbitrated v8 real-data values.
            var config = AssetDatabase.LoadAssetAtPath<TerrainGenerationConfig>(ConfigPath);
            var (objectGuids, veinGuids) = LoadGuidTables();
            var serializer = new GenerationConfigSerializer(objectGuids, veinGuids);

            var root = new JObject
            {
                ["algorithm"] = "VanillaGenerator",
                ["priority"] = 1000,
                ["algorithmParam"] = serializer.SerializeObject(config)
            };

            File.WriteAllText(OutputPath, root.ToString(Formatting.Indented));
            LogWarnings(serializer);
            Debug.Log($"[GenerationConfigExporter] Wrote {OutputPath}");
        }

        // map.json（mapObjects/mapVeins）から name→guid の解決表を構築する
        // Builds name→guid resolution tables from map.json (mapObjects/mapVeins).
        private static (Dictionary<string, string>, Dictionary<string, string>) LoadGuidTables()
        {
            var objectGuids = new Dictionary<string, string>();
            var veinGuids = new Dictionary<string, string>();
            var map = JObject.Parse(File.ReadAllText(MapJsonPath));

            foreach (var o in map["mapObjects"])
                objectGuids[(string)o["mapObjectName"]] = (string)o["mapObjectGuid"];
            foreach (var v in map["mapVeins"])
                veinGuids[(string)v["veinName"]] = (string)v["veinGuid"];
            return (objectGuids, veinGuids);
        }

        private static void LogWarnings(GenerationConfigSerializer serializer)
        {
            // P1では未整備のアドレス欄と未解決プレハブを警告として明示する
            // Warn-logs the not-yet-populated address fields and unresolved prefabs (P1 scope).
            if (serializer.EmptyAssetFields.Count > 0)
                Debug.LogWarning($"[GenerationConfigExporter] Empty addressablePath fields ({serializer.EmptyAssetFields.Count}): "
                                 + string.Join(", ", serializer.EmptyAssetFields));
            if (serializer.UnmatchedPrefabs.Count > 0)
                Debug.LogWarning($"[GenerationConfigExporter] Unmatched prefabs -> empty guid ({serializer.UnmatchedPrefabs.Count}): "
                                 + string.Join(", ", serializer.UnmatchedPrefabs));
        }
    }
}
