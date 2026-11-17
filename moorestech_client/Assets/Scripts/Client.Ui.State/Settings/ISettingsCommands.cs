using Client.Game.InGame.GraphicsQuality;
using Client.Input;

namespace Client.Ui.State.Settings
{
    /// <summary>
    /// 設定画面の操作。Web のアダプタと GameUi の View が同じ口を呼ぶ
    /// Settings-screen operations; the Web adapters and the GameUi views call the same entry points
    /// </summary>
    public interface ISettingsCommands
    {
        void ApplyGraphicsPreset(GraphicsQualityPreset preset);
        void SetGraphicsQuality(GraphicsQualitySettings settings);
        void SetSprintInputMode(SprintInputMode mode);
        void SetLanguage(string languageCode);
    }
}
