using Client.Game.InGame.GraphicsQuality;
using Client.Input;
using Client.Localization;
using Client.Ui.State.Commands;

namespace Client.Ui.State.Settings
{
    /// <summary>
    /// 画質・ダッシュ方式・言語の保存窓口へ渡す型付きコマンド
    /// Typed commands that hand graphics, sprint-mode and language choices to their preference stores
    /// </summary>
    public sealed class SettingsCommands : ISettingsCommands
    {
        public void ApplyGraphicsPreset(GraphicsQualityPreset preset)
        {
            GraphicsQualityPreference.ApplyPreset(preset);
        }

        public void SetGraphicsQuality(GraphicsQualitySettings settings)
        {
            GraphicsQualityPreference.SetSettings(settings);
        }

        public void SetSprintInputMode(SprintInputMode mode)
        {
            SprintInputModePreference.SetMode(mode);
        }

        // 選択可否の判定と拒否のログは Localize が持つ。偽なら不明な言語として拒否する
        // Localize owns the selectability check and logs the rejection; false is rejected as an unknown language
        public UiCommandResult SetLanguage(string languageCode)
        {
            if (Localize.TrySetChosenLanguage(languageCode)) return UiCommandAccepted.Instance;
            return UiCommandRejected.Of(UiCommandRejectReason.UnknownLanguage);
        }
    }
}
