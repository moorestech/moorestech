using System;
using Client.Playtest.Input;
using UnityEngine.InputSystem;

namespace Client.Tests.EditModeInPlayingTest.Ui
{
    /// <summary>
    /// 背面の Editor でも注入したキーボード・マウスが届くよう、開いている間だけ入力設定を切り替える
    /// While open, switches input settings so injected keyboard/mouse input reaches a background Editor
    /// </summary>
    /// <remarks>
    /// 背面の Editor は既定（ResetAndDisableNonBackgroundDevices）でキーボードとマウスを無効化し、QueueStateEvent を捨てる。InteractKeyBlockOpener と同じ切り替え
    /// A background Editor disables keyboard and mouse by default (ResetAndDisableNonBackgroundDevices) and drops QueueStateEvent; same switch as InteractKeyBlockOpener
    /// </remarks>
    public sealed class EditorBackgroundInputScope : IDisposable
    {
        private readonly InputSettings.BackgroundBehavior _savedBackground;
        private readonly InputSettings.EditorInputBehaviorInPlayMode _savedEditorInput;

        private EditorBackgroundInputScope()
        {
            var settings = InputSystem.settings;
            _savedBackground = settings.backgroundBehavior;
            _savedEditorInput = settings.editorInputBehaviorInPlayMode;
            settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;

            SemanticInput.EnsureDevices();
            InputSystem.EnableDevice(Keyboard.current);
            InputSystem.EnableDevice(Mouse.current);
        }

        public static EditorBackgroundInputScope Open()
        {
            return new EditorBackgroundInputScope();
        }

        public void Dispose()
        {
            // silent-ok: DisableDevice を呼ぶと背面の一時無効が恒久無効に変わるため戻さない（次のフォーカス喪失で InputSystem が一時無効へ付け直す）/ 設定値は下で復元し、注入が届かなければ後続テストが失敗で気づく
            // silent-ok: DisableDevice would turn the temporary background-disable into a permanent one, so skip it (InputSystem re-applies the temporary disable on the next focus loss) / settings are restored below and a later test fails if injection stops arriving
            InputSystem.settings.backgroundBehavior = _savedBackground;
            InputSystem.settings.editorInputBehaviorInPlayMode = _savedEditorInput;
        }
    }
}
