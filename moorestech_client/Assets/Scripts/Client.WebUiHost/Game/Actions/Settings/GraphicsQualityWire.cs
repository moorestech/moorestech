using Client.Game.InGame.GraphicsQuality;
using Newtonsoft.Json.Linq;
using UnityEngine;
using System.Numerics;

namespace Client.WebUiHost.Game.Actions
{
    /// <summary>
    /// 画質設定のC#値とWeb契約の相互変換。未知の値は変換失敗にする
    /// Converts graphics quality between C# values and the web contract; unknown values fail conversion
    /// </summary>
    // topicのsettings節。WebUiJsonがcamelCaseで書き出す
    // The settings section of the topic; WebUiJson writes it in camelCase
    internal class GraphicsQualitySettingsData
    {
        public int DrawDistanceMeters;
        public bool Shadows;
        public int RenderScalePercent;
        public bool Ssao;
        public bool PostProcessing;
        public bool Effects;
        public bool MapObjects;
        public string TerrainDetail;
        public bool Ocean;
    }

    internal enum GraphicsQualityParseStatus
    {
        Success, MissingSettings, MissingField, InvalidType, UnsupportedValue,
    }

    internal static class GraphicsQualityWire
    {
        public static string PresetToWire(GraphicsQualitySettings settings)
        {
            if (!GraphicsQualityPresets.TryMatch(settings, out var preset)) return "custom";
            return preset switch { GraphicsQualityPreset.High => "high", GraphicsQualityPreset.Medium => "medium", _ => "low" };
        }

        public static bool TryPresetFromWire(string wire, out GraphicsQualityPreset preset)
        {
            preset = GraphicsQualityPreset.Medium;
            switch (wire)
            {
                case "high": preset = GraphicsQualityPreset.High; return true;
                case "medium": preset = GraphicsQualityPreset.Medium; return true;
                case "low": preset = GraphicsQualityPreset.Low; return true;
                default: return false;
            }
        }

        public static GraphicsQualitySettingsData ToData(GraphicsQualitySettings s)
        {
            return new GraphicsQualitySettingsData
            {
                DrawDistanceMeters = (int)s.DrawDistance.ToMeters(),
                Shadows = s.ShadowsEnabled,
                RenderScalePercent = ToPercent(s.RenderScale),
                Ssao = s.SsaoEnabled,
                PostProcessing = s.PostProcessingEnabled,
                Effects = s.EffectsEnabled,
                MapObjects = s.MapObjectsVisible,
                TerrainDetail = s.TerrainDetail == GraphicsTerrainDetail.Fine ? "fine" : "coarse",
                Ocean = s.OceanVisible,
            };
        }

        public static GraphicsQualityParseStatus Parse(JObject payload, out GraphicsQualitySettings settings, out string invalidField)
        {
            settings = default;
            invalidField = "settings";
            if (payload?["settings"] == null) return GraphicsQualityParseStatus.MissingSettings;
            if (payload["settings"] is not JObject json) return GraphicsQualityParseStatus.InvalidType;

            // 欠落・型・値域別に最初の拒否を返す
            // Distinguish missing fields, types and ranges; return the first rejection
            var keys = new[] { "drawDistanceMeters", "renderScalePercent", "terrainDetail", "shadows", "ssao", "postProcessing", "effects", "mapObjects", "ocean" };
            foreach (var key in keys)
            {
                invalidField = key;
                var token = json[key];
                if (token == null) return GraphicsQualityParseStatus.MissingField;
                var expectedType = key switch
                {
                    "drawDistanceMeters" or "renderScalePercent" => JTokenType.Integer,
                    "terrainDetail" => JTokenType.String,
                    _ => JTokenType.Boolean,
                };
                if (token.Type != expectedType) return GraphicsQualityParseStatus.InvalidType;
                if (expectedType == JTokenType.Integer && !TryInteger(token, out _)) return GraphicsQualityParseStatus.UnsupportedValue;
                if (key == "drawDistanceMeters" && !TryDrawDistance(token, out _)) return GraphicsQualityParseStatus.UnsupportedValue;
                if (key == "renderScalePercent" && !TryRenderScale(token, out _)) return GraphicsQualityParseStatus.UnsupportedValue;
                if (key == "terrainDetail" && token.Value<string>() is not ("fine" or "coarse")) return GraphicsQualityParseStatus.UnsupportedValue;
            }

            TryDrawDistance(json["drawDistanceMeters"], out var drawDistance);
            TryRenderScale(json["renderScalePercent"], out var renderScale);
            var terrain = json["terrainDetail"].Value<string>() == "fine" ? GraphicsTerrainDetail.Fine : GraphicsTerrainDetail.Coarse;
            settings = new GraphicsQualitySettings(drawDistance, json["shadows"].Value<bool>(), renderScale, json["ssao"].Value<bool>(),
                json["postProcessing"].Value<bool>(), json["effects"].Value<bool>(), json["mapObjects"].Value<bool>(), terrain, json["ocean"].Value<bool>());
            invalidField = null;
            return GraphicsQualityParseStatus.Success;

            #region Internal

            // 数値との対応は既存の変換を逆引きする
            // Look up numeric values through the existing conversions
            bool TryDrawDistance(JToken token, out GraphicsDrawDistance value)
            {
                value = default;
                if (!TryInteger(token, out var number)) return false;
                foreach (GraphicsDrawDistance candidate in System.Enum.GetValues(typeof(GraphicsDrawDistance)))
                {
                    if ((int)candidate.ToMeters() != number) continue;
                    value = candidate;
                    return true;
                }
                return false;
            }

            bool TryRenderScale(JToken token, out GraphicsRenderScale value)
            {
                value = default;
                if (!TryInteger(token, out var number)) return false;
                foreach (GraphicsRenderScale candidate in System.Enum.GetValues(typeof(GraphicsRenderScale)))
                {
                    if (ToPercent(candidate) != number) continue;
                    value = candidate;
                    return true;
                }
                return false;
            }

            // 整数箱の種類に依存せず、範囲外の整数も例外なく拒否する
            // Normalize integral boxes and reject oversized integers without exceptions
            bool TryInteger(JToken token, out long value)
            {
                value = 0;
                if (token is not JValue { Type: JTokenType.Integer } integer) return false;
                switch (integer.Value)
                {
                    case sbyte n: value = n; return true;
                    case byte n: value = n; return true;
                    case short n: value = n; return true;
                    case ushort n: value = n; return true;
                    case int n: value = n; return true;
                    case uint n: value = n; return true;
                    case long n: value = n; return true;
                    case ulong n when n <= (ulong)long.MaxValue: value = (long)n; return true;
                    case BigInteger n when long.MinValue <= n && n <= long.MaxValue: value = (long)n; return true;
                    default: return false;
                }
            }

            #endregion
        }

        private static int ToPercent(GraphicsRenderScale renderScale)
        {
            return Mathf.RoundToInt(renderScale.ToScale() * 100f);
        }
    }
}
