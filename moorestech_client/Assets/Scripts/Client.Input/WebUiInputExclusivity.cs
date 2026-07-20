using UnityEngine;

namespace Client.Input
{
    [System.Flags]
    public enum InputSuppressionScope
    {
        Keyboard = 1,
    }

    /// <summary>
    /// Web UIの状態保持とテキスト入力時のキー抑止を担う。
    /// Holds Web UI input state and suppresses keyboard input while a text field is focused.
    /// </summary>
    public static class WebUiInputExclusivity
    {
        private static volatile bool _pointerOverUi;
        private static volatile bool _textInputFocused;
        private static int _lastKeyboardProbeFrame = -1;

        public static bool IsPointerOverWebUi => _pointerOverUi;

        public static void SetState(bool pointerOverUi, bool textInputFocused)
        {
            _pointerOverUi = pointerOverUi;
            _textInputFocused = textInputFocused;
        }

        public static bool IsSuppressed(InputSuppressionScope scope)
        {
            return (scope & InputSuppressionScope.Keyboard) != 0 && _textInputFocused;
        }

        public static void ProbeSuppressed(InputSuppressionScope scope)
        {
            // 入力時だけ記録しログ洪水を防ぐ
            // Log only on input to prevent log floods
            if ((scope & InputSuppressionScope.Keyboard) != 0 && _textInputFocused && _lastKeyboardProbeFrame != Time.frameCount)
            {
                _lastKeyboardProbeFrame = Time.frameCount;
                Debug.Log("[WebUiInputProbe] Suppressed keyboard input because a Web text field owns focus");
            }
        }
    }
}
