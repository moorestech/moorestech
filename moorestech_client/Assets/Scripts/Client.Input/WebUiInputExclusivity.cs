using UnityEngine;

namespace Client.Input
{
    [System.Flags]
    public enum InputSuppressionScope
    {
        Keyboard = 1,
        Pointer = 2,
    }

    /// <summary>
    /// Web UIが所有する入力範囲を一元管理し、ゲーム入力の読み取り口で抑止する。
    /// Owns the Web UI input claim and suppresses game input at its read boundary.
    /// </summary>
    public static class WebUiInputExclusivity
    {
        private static volatile bool _pointerOverUi;
        private static volatile bool _textInputFocused;
        private static int _lastPointerProbeFrame = -1;
        private static int _lastKeyboardProbeFrame = -1;

        public static void SetState(bool pointerOverUi, bool textInputFocused)
        {
            _pointerOverUi = pointerOverUi;
            _textInputFocused = textInputFocused;
        }

        public static bool IsSuppressed(InputSuppressionScope scope)
        {
            return ((scope & InputSuppressionScope.Pointer) != 0 && _pointerOverUi) ||
                   ((scope & InputSuppressionScope.Keyboard) != 0 && _textInputFocused);
        }

        public static void ProbeSuppressed(InputSuppressionScope scope)
        {
            // 物理入力が存在したフレームだけカテゴリ別に一度記録し、長押しによるログ洪水を防ぐ
            // Log once per category only on frames with physical input, avoiding held-input log floods
            if ((scope & InputSuppressionScope.Pointer) != 0 && _pointerOverUi && _lastPointerProbeFrame != Time.frameCount)
            {
                _lastPointerProbeFrame = Time.frameCount;
                Debug.Log("[WebUiInputProbe] Suppressed pointer input because the pointer is over Web UI");
            }
            if ((scope & InputSuppressionScope.Keyboard) != 0 && _textInputFocused && _lastKeyboardProbeFrame != Time.frameCount)
            {
                _lastKeyboardProbeFrame = Time.frameCount;
                Debug.Log("[WebUiInputProbe] Suppressed keyboard input because a Web text field owns focus");
            }
        }
    }
}
