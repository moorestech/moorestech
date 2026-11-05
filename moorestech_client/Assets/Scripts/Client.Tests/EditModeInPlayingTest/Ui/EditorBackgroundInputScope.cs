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
        private readonly bool _keyboardWasEnabled;
        private readonly bool _mouseWasEnabled;

        private EditorBackgroundInputScope()
        {
            var settings = InputSystem.settings;
            _savedBackground = settings.backgroundBehavior;
            _savedEditorInput = settings.editorInputBehaviorInPlayMode;
            settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;

            SemanticInput.EnsureDevices();
            _keyboardWasEnabled = Keyboard.current.enabled;
            _mouseWasEnabled = Mouse.current.enabled;
            InputSystem.EnableDevice(Keyboard.current);
            InputSystem.EnableDevice(Mouse.current);
        }

        public static EditorBackgroundInputScope Open()
        {
            return new EditorBackgroundInputScope();
        }

        public void Dispose()
        {
            if (!_keyboardWasEnabled) InputSystem.DisableDevice(Keyboard.current);
            if (!_mouseWasEnabled) InputSystem.DisableDevice(Mouse.current);
            InputSystem.settings.backgroundBehavior = _savedBackground;
            InputSystem.settings.editorInputBehaviorInPlayMode = _savedEditorInput;
        }
    }
}
