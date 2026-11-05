using Client.Game.InGame.GraphicsQuality;
using Client.Input;

namespace Client.Ui.State.Settings
{
    // 画質プリセットの表示上の選択。どのプリセットとも一致しなければ Custom
    // The preset shown as selected; Custom when the settings match no preset
    public enum GraphicsQualityPresetChoice
    {
        High,
        Medium,
        Low,
        Custom,
    }

    /// <summary>
    /// 設定画面の表示に要る値の不変スナップショット
    /// Immutable snapshot of the values the settings screen displays
    /// </summary>
    public sealed class SettingsViewModel
    {
        public GraphicsQualitySettings Graphics { get; }
        public GraphicsQualityPresetChoice Preset { get; }
        public bool GraphicsRecovered { get; }
        public SprintInputMode SprintMode { get; }
        public bool SprintRecovered { get; }
        public string LanguageCode { get; }

        public SettingsViewModel(GraphicsQualitySettings graphics, GraphicsQualityPresetChoice preset, bool graphicsRecovered, SprintInputMode sprintMode, bool sprintRecovered, string languageCode)
        {
            Graphics = graphics;
            Preset = preset;
            GraphicsRecovered = graphicsRecovered;
            SprintMode = sprintMode;
            SprintRecovered = sprintRecovered;
            LanguageCode = languageCode;
        }
    }
}
