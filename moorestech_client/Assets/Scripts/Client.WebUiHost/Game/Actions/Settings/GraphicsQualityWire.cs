using Client.Game.InGame.GraphicsQuality;
using Newtonsoft.Json.Linq;
using UnityEngine;

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

        public static bool TryFromJObject(JObject json, out GraphicsQualitySettings settings)
        {
            settings = default;
            if (json == null) return false;
            if (!TryDrawDistance(json["drawDistanceMeters"], out var drawDistance) || !TryRenderScale(json["renderScalePercent"], out var renderScale) ||
                !TryTerrain(json["terrainDetail"], out var terrain) || !TryBool(json["shadows"], out var shadows) || !TryBool(json["ssao"], out var ssao) ||
                !TryBool(json["postProcessing"], out var post) || !TryBool(json["effects"], out var effects) ||
                !TryBool(json["mapObjects"], out var mapObjects) || !TryBool(json["ocean"], out var ocean)) return false;
            settings = new GraphicsQualitySettings(drawDistance, shadows, renderScale, ssao, post, effects, mapObjects, terrain, ocean);
            return true;

            #region Internal

            bool TryBool(JToken token, out bool value)
            {
                value = false;
                if (token == null || token.Type != JTokenType.Boolean) return false;
                value = token.Value<bool>();
                return true;
            }

            // 数値との対応はToMeters/ToScaleを正とし、ここでは逆引きだけする
            // ToMeters/ToScale own the numeric mapping; this only looks it up in reverse
            bool TryDrawDistance(JToken token, out GraphicsDrawDistance value)
            {
                value = GraphicsDrawDistance.Meters300;
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
                value = GraphicsRenderScale.Percent100;
                if (!TryInteger(token, out var number)) return false;
                foreach (GraphicsRenderScale candidate in System.Enum.GetValues(typeof(GraphicsRenderScale)))
                {
                    if (ToPercent(candidate) != number) continue;
                    value = candidate;
                    return true;
                }
                return false;
            }

            bool TryTerrain(JToken token, out GraphicsTerrainDetail value)
            {
                value = GraphicsTerrainDetail.Fine;
                var text = token?.Type == JTokenType.String ? token.Value<string>() : null;
                if (text == "fine") return true;
                value = GraphicsTerrainDetail.Coarse;
                return text == "coarse";
            }

            // longに収まらない整数（BigInteger）はValue<int>が例外を投げるので、型で弾いて失敗契約へ流す
            // Integers beyond long (BigInteger) make Value<int> throw, so reject by type and let the failure contract answer
            bool TryInteger(JToken token, out long value)
            {
                value = 0;
                if (token is not JValue { Type: JTokenType.Integer, Value: long number }) return false;
                value = number;
                return true;
            }

            #endregion
        }

        private static int ToPercent(GraphicsRenderScale renderScale)
        {
            return Mathf.RoundToInt(renderScale.ToScale() * 100f);
        }
    }
}
