using UnityEngine.InputSystem;

namespace Client.Playtest.Core
{
    /// <summary>
    ///     Game Viewのフォーカスを一切動かさずに、注入入力がゲームへ届く状態をシナリオ実行中だけ作る
    ///     Makes injected input reach the game for the duration of a scenario without ever moving Game View focus
    /// </summary>
    public static class PlaytestInputFocusOverride
    {
        private static InputSettings.BackgroundBehavior _savedBackgroundBehavior;
        private static InputSettings.EditorInputBehaviorInPlayMode _savedEditorInputBehavior;
        private static bool _isOverriding;

        // InputSystemの既定(PointersAndKeyboardsRespectGameViewFocus)はGame View非フォーカス時に注入キーを捨てる
        // The Input System default (PointersAndKeyboardsRespectGameViewFocus) drops injected keys while the Game View is unfocused
        // IgnoreFocus + AllDeviceInputAlwaysGoesToGameView の組でのみgameShouldGetInputRegardlessOfFocusが立つ（InputManager.cs:409）
        // Only the IgnoreFocus + AllDeviceInputAlwaysGoesToGameView pair raises gameShouldGetInputRegardlessOfFocus (InputManager.cs:409)
        public static void Enable()
        {
            if (_isOverriding) return;

            var settings = InputSystem.settings;
            _savedBackgroundBehavior = settings.backgroundBehavior;
            _savedEditorInputBehavior = settings.editorInputBehaviorInPlayMode;
            _isOverriding = true;

            settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;

            // 設定変更は既に無効化済みのデバイスを起こさないため、明示的に再有効化する
            // Changing the settings does not wake devices that were already disabled, so re-enable them explicitly
            foreach (var device in InputSystem.devices)
            {
                if (!device.enabled) InputSystem.EnableDevice(device);
            }
        }

        public static void Restore()
        {
            if (!_isOverriding) return;

            var settings = InputSystem.settings;
            settings.backgroundBehavior = _savedBackgroundBehavior;
            settings.editorInputBehaviorInPlayMode = _savedEditorInputBehavior;
            _isOverriding = false;
        }
    }
}
