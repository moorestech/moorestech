using Client.Game.InGame.GraphicsQuality;

namespace Client.Ui.State.Settings
{
    /// <summary>
    /// 画質の設定値から選択中のプリセットを導く唯一の規則。UITK の ViewModel と Web の topic が共有する
    /// The single rule that derives the selected preset from graphics settings; shared by the UITK view model and the Web topic
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
