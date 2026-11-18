using Client.Game.InGame.GraphicsQuality;
using Client.Input;
using Client.Tests.Ui.TestDoubles;
using Client.Ui.State.Settings;

namespace Client.Tests.Ui.Screens.Settings
{
    /// <summary>
    /// 設定画面の偽モデル。テストが Set で ViewModel を差し込む
    /// Fake settings model; tests push view models with Set
    /// </summary>
    public sealed class FakeSettingsModel : FakeModel<SettingsViewModel>
    {
        public FakeSettingsModel(SettingsViewModel initial) : base(initial)
        {
        }

        // 中プリセット・トグル・英語・復旧印なしの ViewModel
        // View model with the medium preset, toggle sprint, English and no recovery notices
        public static SettingsViewModel Default()
        {
            return new SettingsViewModel(GraphicsQualityPresets.Get(GraphicsQualityPreset.Medium), GraphicsQualityPresetChoice.Medium, false, SprintInputMode.Toggle, false, "english");
        }
    }
}
