using Client.Input;
using UnityEngine;

namespace Client.Game.InGame.Hotbar
{
    /// <summary>
    ///     数字キー入力をタップと長押し（0.5秒）に判別する共通ヘルパ
    ///     Shared helper that classifies digit-key input as a tap or a long press (0.5s)
    /// </summary>
    public static class HotbarKeyInput
    {
        private const float LongPressThresholdSeconds = 0.5f;

        // 現在保持中のスロット。何も押されていなければ-1
        // The slot currently held; -1 when nothing is pressed
        private static int _heldSlot = -1;
        private static float _pressStartTime;
        private static bool _longPressFired;

        private static bool _tapPending;
        private static int _tapSlot;
        private static bool _longPressPending;
        private static int _longPressSlot;

        // タップ確定（閾値未満で離された）を1回だけ消費する
        // Consumes a confirmed tap (released before the threshold) exactly once
        public static bool TryGetTappedSlot(out int slot)
        {
            Poll();

            if (_tapPending)
            {
                slot = _tapSlot;
                _tapPending = false;
                return true;
            }

            slot = default;
            return false;
        }

        // 長押し成立（保持中に閾値へ到達）を1回だけ消費する
        // Consumes a fired long press (threshold reached while still held) exactly once
        public static bool TryGetLongPressedSlot(out int slot)
        {
            Poll();

            if (_longPressPending)
            {
                slot = _longPressSlot;
                _longPressPending = false;
                return true;
            }

            slot = default;
            return false;
        }

        // 押下中キーの継続状態を1フレーム分進め、タップ/長押しの成立を検出する
        // Advances the currently-held key's state by one frame and detects a tap or long-press event
        private static void Poll()
        {
            var rawValue = InputManager.UI.HotBar.ReadValue<int>();
            var currentSlot = rawValue == 0 ? -1 : rawValue - 1;

            if (currentSlot != _heldSlot)
            {
                HandleHeldSlotChanged(currentSlot);
                return;
            }

            TryFireLongPress();

            #region Internal

            // 保持キーが切り替わった/離された処理。閾値未満での離しだけタップとして確定する
            // Handles a held-key change/release; only a release before the threshold confirms a tap
            void HandleHeldSlotChanged(int nextSlot)
            {
                if (_heldSlot != -1 && !_longPressFired)
                {
                    _tapPending = true;
                    _tapSlot = _heldSlot;
                }

                _heldSlot = nextSlot;
                _pressStartTime = Time.unscaledTime;
                _longPressFired = false;
            }

            // 保持継続中に閾値へ到達したら長押しを1度だけ成立させる
            // Fires the long press exactly once when the threshold is reached while still held
            void TryFireLongPress()
            {
                if (_heldSlot == -1 || _longPressFired) return;
                if (Time.unscaledTime - _pressStartTime < LongPressThresholdSeconds) return;

                _longPressFired = true;
                _longPressPending = true;
                _longPressSlot = _heldSlot;
            }

            #endregion
        }
    }
}
