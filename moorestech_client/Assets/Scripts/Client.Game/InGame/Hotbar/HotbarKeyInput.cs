namespace Client.Game.InGame.Hotbar
{
    /// <summary>
    ///     数字キーの押下をタップと長押し（0.5秒）に判別する状態機械。入力読取と時刻は呼び出し側がプッシュする
    ///     State machine classifying digit-key presses as a tap or a long press (0.5s); the caller pushes the input and the clock
    /// </summary>
    public class HotbarKeyInput
    {
        private const float LongPressThresholdSeconds = 0.5f;

        // 現在保持中のスロット。何も押されていなければnull
        // The slot currently held; null when nothing is pressed
        private int? _heldSlot;
        private float _pressStartTime;
        private bool _longPressFired;

        private bool _tapPending;
        private int _tapSlot;
        private bool _longPressPending;
        private int _longPressSlot;

        // 押下中キーの継続状態を1フレーム分進め、タップ/長押しの成立を検出する
        // Advances the currently-held key's state by one frame and detects a tap or long-press event
        public void ManualUpdate(int? heldSlot, float unscaledTime)
        {
            if (heldSlot != _heldSlot)
            {
                HandleHeldSlotChanged(heldSlot);
                return;
            }

            TryFireLongPress();

            #region Internal

            // 保持キーが切り替わった/離された処理。閾値未満での離しだけタップとして確定する
            // Handles a held-key change/release; only a release before the threshold confirms a tap
            void HandleHeldSlotChanged(int? nextSlot)
            {
                if (_heldSlot.HasValue && !_longPressFired)
                {
                    _tapPending = true;
                    _tapSlot = _heldSlot.Value;
                }

                _heldSlot = nextSlot;
                _pressStartTime = unscaledTime;
                _longPressFired = false;
            }

            // 保持継続中に閾値へ到達したら長押しを1度だけ成立させる
            // Fires the long press exactly once when the threshold is reached while still held
            void TryFireLongPress()
            {
                if (!_heldSlot.HasValue || _longPressFired) return;
                if (unscaledTime - _pressStartTime < LongPressThresholdSeconds) return;

                _longPressFired = true;
                _longPressPending = true;
                _longPressSlot = _heldSlot.Value;
            }

            #endregion
        }

        // タップ確定（閾値未満で離された）を1回だけ消費する
        // Consumes a confirmed tap (released before the threshold) exactly once
        public bool TryGetTappedSlot(out int slot)
        {
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
        public bool TryGetLongPressedSlot(out int slot)
        {
            if (_longPressPending)
            {
                slot = _longPressSlot;
                _longPressPending = false;
                return true;
            }

            slot = default;
            return false;
        }

        // UIStateを跨いだ保持状態を破棄する。ManualUpdateが呼ばれないUIState滞在中は経過時間が進まないため、
        // 復帰直後に古い押下開始時刻へ基づく誤長押し判定が起きないよう、遷移のたびリセットする
        // Discards held-key state across UIStates. Elapsed time freezes while a UIState that never calls ManualUpdate is active,
        // so this is reset on every transition to avoid a stale press-start time firing a false long press
        public void Reset()
        {
            _heldSlot = null;
            _longPressFired = false;
            _tapPending = false;
            _longPressPending = false;
        }
    }
}
