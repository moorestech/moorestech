namespace Client.Game.InGame.GraphicsQuality
{
    /// <summary>
    /// 高/中/低の値表。中は描画距離だけを縮め、低は全項目を最低値にする（v7の残りは各項目の最低値が担う）
    /// The high/medium/low value table; medium only shortens the draw distance, low puts every item at its lowest (the rest of v7 rides on those lowest values)
    /// </summary>
    public static class GraphicsQualityPresets
    {
        private static readonly GraphicsQualityPreset[] AllPresets = { GraphicsQualityPreset.High, GraphicsQualityPreset.Medium, GraphicsQualityPreset.Low };

        public static GraphicsQualitySettings Get(GraphicsQualityPreset preset)
        {
            return preset switch
            {
                GraphicsQualityPreset.High => new GraphicsQualitySettings(GraphicsDrawDistance.Meters5000, true, GraphicsRenderScale.Percent100, true, true, true, true, GraphicsTerrainDetail.Fine, true),
                GraphicsQualityPreset.Medium => new GraphicsQualitySettings(GraphicsDrawDistance.Meters300, true, GraphicsRenderScale.Percent100, true, true, true, true, GraphicsTerrainDetail.Fine, true),
                GraphicsQualityPreset.Low => new GraphicsQualitySettings(GraphicsDrawDistance.Meters300, false, GraphicsRenderScale.Percent50, false, false, false, false, GraphicsTerrainDetail.Coarse, false),
                _ => throw new System.ArgumentOutOfRangeException(nameof(preset), preset, null),
            };
        }

        // どのプリセットとも一致しなければfalse（設定画面ではカスタム表示）
        // Returns false when no preset matches (shown as custom on the settings screen)
        public static bool TryMatch(GraphicsQualitySettings settings, out GraphicsQualityPreset matched)
        {
            foreach (var preset in AllPresets)
            {
                if (!Get(preset).Equals(settings)) continue;
                matched = preset;
                return true;
            }
            matched = GraphicsQualityPreset.Medium;
            return false;
        }
    }
}
