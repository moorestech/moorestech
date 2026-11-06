using Client.Game.InGame.GraphicsQuality;

namespace Client.Ui.State.Settings
{
    /// <summary>
    /// 選択中プリセットを導く唯一の規則
    /// The single rule deriving the selected preset
    /// </summary>
    public static class GraphicsQualityPresetChoices
    {
        public static GraphicsQualityPresetChoice From(GraphicsQualitySettings settings)
        {
            if (!GraphicsQualityPresets.TryMatch(settings, out var preset)) return GraphicsQualityPresetChoice.Custom;
            return preset switch
            {
                GraphicsQualityPreset.High => GraphicsQualityPresetChoice.High,
                GraphicsQualityPreset.Medium => GraphicsQualityPresetChoice.Medium,
                GraphicsQualityPreset.Low => GraphicsQualityPresetChoice.Low,
                _ => throw new System.ArgumentOutOfRangeException(nameof(preset), preset, null),
            };
        }
    }
}
