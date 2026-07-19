using Cysharp.Threading.Tasks;
using CefUnity.Interop;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace Client.Playtest.WebUi
{
    /// <summary>
    ///     InputSystemへ注入されたプレイテスト用マウス状態をCEFへ転送する
    ///     Forwards playtest mouse state injected into InputSystem to CEF
    ///
    ///     Client.PlaytestはEditor専用asmdefでMonoBehaviourを実行時GameObjectへ付与できないため、
    ///     UniTaskのフレームループとして駆動する静的クラスにしている。
    ///     Client.Playtest is an editor-only asmdef whose MonoBehaviours cannot attach to runtime GameObjects,
    ///     so this runs as a static class driven by a UniTask per-frame loop instead.
    /// </summary>
    public static class CefInputForwarder
    {
        private static bool _running;
        private static Vector2 _lastScreenPosition;
        private static bool _hasLastScreenPosition;
        private static float _wheelAccumX;
        private static float _wheelAccumY;

        public static void StartForwarding()
        {
            if (_running) return;
            _running = true;
            _hasLastScreenPosition = false;
            _wheelAccumX = 0f;
            _wheelAccumY = 0f;
            RunLoop().Forget();

            #region Internal

            async UniTaskVoid RunLoop()
            {
                // Update相当のタイミングで毎フレーム1回だけ読む（wasPressedThisFrameの1フレーム判定を保つ）
                // Read exactly once per frame at Update timing (preserves wasPressedThisFrame single-frame semantics)
                while (_running && Application.isPlaying)
                {
                    ForwardFrame();
                    await UniTask.Yield(PlayerLoopTiming.Update);
                }
                _running = false;
            }

            #endregion
        }

        public static void StopForwarding()
        {
            _running = false;
        }

        private static void ForwardFrame()
        {
            var mouse = Mouse.current;
            if (mouse == null || !CefScreenMapper.TryGetBrowser(out var browser)) return;

            var screenPosition = mouse.position.ReadValue();
            if (!CefScreenMapper.TryScreenToBrowser(screenPosition, out var browserPosition)) return;
            var modifiers = GetModifiers();

            // Editor非フォーカスのプレイテストではOS側legacy入力が動かず、パッケージとの二重送信は発生しない
            // In unfocused Editor playtests OS legacy input stays idle, so the package does not double-send these events
            if (!_hasLastScreenPosition || screenPosition != _lastScreenPosition)
            {
                _lastScreenPosition = screenPosition;
                _hasLastScreenPosition = true;
                browser.SendMouseMove(browserPosition.x, browserPosition.y, modifiers);
            }

            // 押下と解放を左右中の各ボタンで同じCEF ingressへ明示的に転送する
            // Forward press and release for left, right, and middle buttons through the same CEF ingress
            ForwardButton(mouse.leftButton, MouseButton.Left);
            ForwardButton(mouse.rightButton, MouseButton.Right);
            ForwardButton(mouse.middleButton, MouseButton.Middle);

            // 小数ホイール量を蓄積し、CEFへ送った整数部だけを差し引く
            // Accumulate fractional wheel input and subtract only the integer part sent to CEF
            var scroll = mouse.scroll.ReadValue();
            _wheelAccumX += scroll.x;
            _wheelAccumY += scroll.y;
            var wheelDeltaX = (int)_wheelAccumX;
            var wheelDeltaY = (int)_wheelAccumY;
            if (wheelDeltaX == 0 && wheelDeltaY == 0) return;

            _wheelAccumX -= wheelDeltaX;
            _wheelAccumY -= wheelDeltaY;
            browser.SendMouseWheel(browserPosition.x, browserPosition.y, wheelDeltaX, wheelDeltaY, modifiers);

            #region Internal

            void ForwardButton(ButtonControl button, MouseButton mouseButton)
            {
                if (button.wasPressedThisFrame)
                {
                    browser.SendMouseClick(browserPosition.x, browserPosition.y, mouseButton, false, 1, modifiers);
                }
                if (button.wasReleasedThisFrame)
                {
                    browser.SendMouseClick(browserPosition.x, browserPosition.y, mouseButton, true, 1, modifiers);
                }
            }

            uint GetModifiers()
            {
                uint innerModifiers = 0;
                if (mouse.leftButton.isPressed) innerModifiers |= (uint)CefEventFlags.LeftMouseDown;
                if (mouse.rightButton.isPressed) innerModifiers |= (uint)CefEventFlags.RightMouseDown;
                if (mouse.middleButton.isPressed) innerModifiers |= (uint)CefEventFlags.MiddleMouseDown;

                // InputSystemキーボードの修飾キーもCEFのevent flagsへ合成する
                // Merge InputSystem keyboard modifiers into the CEF event flags too
                var keyboard = Keyboard.current;
                if (keyboard == null) return innerModifiers;
                if (keyboard.shiftKey.isPressed) innerModifiers |= (uint)CefEventFlags.ShiftDown;
                if (keyboard.ctrlKey.isPressed) innerModifiers |= (uint)CefEventFlags.ControlDown;
                if (keyboard.altKey.isPressed) innerModifiers |= (uint)CefEventFlags.AltDown;
                if (keyboard.leftCommandKey.isPressed || keyboard.rightCommandKey.isPressed) innerModifiers |= (uint)CefEventFlags.CommandDown;
                return innerModifiers;
            }

            #endregion
        }
    }
}
