using Client.Game.InGame.GraphicsQuality;
using Client.Input;
using Client.Localization;
using UniRx;
using VContainer.Unity;

namespace Client.Ui.State.Settings
{
    /// <summary>
    /// 画質・ダッシュ方式・言語の変化を購読し、ViewModel 1 つへまとめて配る
    /// Subscribes to graphics, sprint-mode and language changes and publishes them as one view model
    /// </summary>
    public sealed class SettingsModel : IInitializable
    {
        private readonly ReactiveProperty<SettingsViewModel> _current = new(Build());

        public IReadOnlyReactiveProperty<SettingsViewModel> Current => _current;

        public void Initialize()
        {
            // 3 つの源はどれも変化だけを流す Subject なので、そのまま組み直す
            // All three sources are change-only subjects, so rebuild on each emission
            GraphicsQualityPreference.OnSettingsChanged.Subscribe(_ => Rebuild());
            SprintInputModePreference.OnSprintInputModeChanged.Subscribe(_ => Rebuild());
            Localize.OnLanguageChanged.Subscribe(_ => Rebuild());

            // ctor から購読までの間の変化を取り込む
            // Pick up any change made between construction and subscription
            Rebuild();
        }

        private void Rebuild()
        {
            _current.Value = Build();
        }

        private static SettingsViewModel Build()
        {
            // 保存値を読んでから復旧印を読む。読み込みが不正値を直したときに印が立つため
            // Read the saved value before the recovery flag, since loading is what raises the flag on repair
            var graphics = GraphicsQualityPreference.Current;
            var sprintMode = SprintInputModePreference.Current;
            return new SettingsViewModel(
                graphics,
                ToPresetChoice(graphics),
                GraphicsQualityPreference.RecoveredInvalidPreference,
                sprintMode,
                SprintInputModePreference.RecoveredInvalidPreference,
                Localize.GetCurrentLanguageCode());
        }

        private static GraphicsQualityPresetChoice ToPresetChoice(GraphicsQualitySettings settings)
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
