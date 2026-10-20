using System;

namespace Client.Game.InGame.GraphicsQuality
{
    /// <summary>
    /// 画質設定の全項目の値。プリセットとの一致判定に値の等価を使う
    /// Every graphics-quality item value; value equality decides whether it matches a preset
    /// </summary>
    public readonly struct GraphicsQualitySettings : IEquatable<GraphicsQualitySettings>
    {
        public GraphicsDrawDistance DrawDistance { get; }
        public bool ShadowsEnabled { get; }
        public GraphicsRenderScale RenderScale { get; }
        public bool SsaoEnabled { get; }
        public bool PostProcessingEnabled { get; }
        public bool EffectsEnabled { get; }
        public bool MapObjectsVisible { get; }
        public GraphicsTerrainDetail TerrainDetail { get; }
        public bool OceanVisible { get; }

        public GraphicsQualitySettings(GraphicsDrawDistance drawDistance, bool shadowsEnabled, GraphicsRenderScale renderScale, bool ssaoEnabled,
            bool postProcessingEnabled, bool effectsEnabled, bool mapObjectsVisible, GraphicsTerrainDetail terrainDetail, bool oceanVisible)
        {
            DrawDistance = drawDistance;
            ShadowsEnabled = shadowsEnabled;
            RenderScale = renderScale;
            SsaoEnabled = ssaoEnabled;
            PostProcessingEnabled = postProcessingEnabled;
            EffectsEnabled = effectsEnabled;
            MapObjectsVisible = mapObjectsVisible;
            TerrainDetail = terrainDetail;
            OceanVisible = oceanVisible;
        }

        public bool Equals(GraphicsQualitySettings other)
        {
            return DrawDistance == other.DrawDistance && ShadowsEnabled == other.ShadowsEnabled && RenderScale == other.RenderScale &&
                   SsaoEnabled == other.SsaoEnabled && PostProcessingEnabled == other.PostProcessingEnabled && EffectsEnabled == other.EffectsEnabled &&
                   MapObjectsVisible == other.MapObjectsVisible && TerrainDetail == other.TerrainDetail && OceanVisible == other.OceanVisible;
        }

        public override bool Equals(object obj)
        {
            return obj is GraphicsQualitySettings other && Equals(other);
        }

        public override int GetHashCode()
        {
            var hash = new HashCode();
            hash.Add(DrawDistance);
            hash.Add(ShadowsEnabled);
            hash.Add(RenderScale);
            hash.Add(SsaoEnabled);
            hash.Add(PostProcessingEnabled);
            hash.Add(EffectsEnabled);
            hash.Add(MapObjectsVisible);
            hash.Add(TerrainDetail);
            hash.Add(OceanVisible);
            return hash.ToHashCode();
        }
    }
}
