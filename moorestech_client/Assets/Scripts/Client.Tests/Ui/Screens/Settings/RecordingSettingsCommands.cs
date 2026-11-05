using Client.Game.InGame.GraphicsQuality;
using Client.Input;
using Client.Tests.Ui.TestDoubles;
using Client.Ui.State.Commands;
using Client.Ui.State.Settings;

namespace Client.Tests.Ui.Screens.Settings
{
    public enum SettingsCommandKind
    {
        ApplyGraphicsPreset,
        SetGraphicsQuality,
        SetSprintInputMode,
        SetLanguage,
    }

    // 発行された設定のコマンド 1 件。値は該当するコマンドだけが持つ
    // One issued settings command; each value is set only by the command that carries it
    public readonly struct SettingsCommandRecord
    {
        public readonly SettingsCommandKind Kind;
        public readonly GraphicsQualityPreset Preset;
        public readonly GraphicsQualitySettings Graphics;
        public readonly SprintInputMode SprintMode;
        public readonly string LanguageCode;

        public SettingsCommandRecord(SettingsCommandKind kind, GraphicsQualityPreset preset, GraphicsQualitySettings graphics, SprintInputMode sprintMode, string languageCode)
        {
            Kind = kind;
            Preset = preset;
            Graphics = graphics;
            SprintMode = sprintMode;
            LanguageCode = languageCode;
        }
    }

    /// <summary>
    /// 設定のコマンドの記録係。言語の結果はテストが SetLanguageResult で決める（既定は受理）
    /// Recorder of settings commands; the language result is chosen by the test via SetLanguageResult (accepted by default)
    /// </summary>
    public sealed class RecordingSettingsCommands : CommandRecorder<SettingsCommandRecord>, ISettingsCommands
    {
        private UiCommandResult _languageResult = UiCommandAccepted.Instance;

        public void ApplyGraphicsPreset(GraphicsQualityPreset preset) => Record(new SettingsCommandRecord(SettingsCommandKind.ApplyGraphicsPreset, preset, default, default, null));
        public void SetGraphicsQuality(GraphicsQualitySettings settings) => Record(new SettingsCommandRecord(SettingsCommandKind.SetGraphicsQuality, default, settings, default, null));
        public void SetSprintInputMode(SprintInputMode mode) => Record(new SettingsCommandRecord(SettingsCommandKind.SetSprintInputMode, default, default, mode, null));

        public UiCommandResult SetLanguage(string languageCode)
        {
            Record(new SettingsCommandRecord(SettingsCommandKind.SetLanguage, default, default, default, languageCode));
            return _languageResult;
        }

        public void SetLanguageResult(UiCommandResult result)
        {
            _languageResult = result;
        }
    }
}
