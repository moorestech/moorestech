namespace Client.Game.InGame.Hotbar
{
    /// <summary>
    ///     数字キーの押下をタップと長押し（0.5秒）に判別する状態機械。入力読取と時刻は呼び出し側がプッシュする
    ///     State machine classifying digit-key presses as a tap or a long press (0.5s); the caller pushes the input and the clock
    /// </summary>
    public class HotbarKeyInput
    {
        private const float LongPressThresholdSeconds = 0.5f;

        // 保持中スロット(未押下ならnull)
        // The slot currently held; null when nothing is pressed
        private int? _heldSlot;
        private float _pressStartTime;
        private bool _longPressFired;

        // Reset時に押下中だった枠。離されるまで再武装させない
        // The slot held at Reset time; it must not re-arm until the key is released
        private int? _deadSlot;

        private bool _tapPending;
        private int _tapSlot;
        private bool _longPressPending;
        private int _longPressSlot;

        // 押下継続を1フレーム進めタップ/長押しを検出
        // Advances the currently-held key's state by one frame and detects a tap or long-press event
        public void ManualUpdate(int? heldSlot, float unscaledTime)
        {
            var activeSlot = ResolveActiveSlot();
            if (activeSlot != _heldSlot)
            {
                HandleHeldSlotChanged(activeSlot);
                return;
            }

            TryFireLongPress();

            #region Internal

            // Resetで消費済みにした押下は、物理的に離されるまで未押下として扱う
            // A press marked consumed by Reset is reported as "no key" until it is physically released
            int? ResolveActiveSlot()
            {
                if (_deadSlot == heldSlot) return null;

                _deadSlot = null;
                return heldSlot;
            }

            // 保持キー変更/離脱の処理。閾値未満の離しでタップ確定
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

        // 押下中の枠を消費済みにし、遷移直後の誤タップ・二重長押しを防ぐ
        // Marks the held slot consumed so a transition cannot produce a false tap or a second long press
        public void Reset()
        {
            _deadSlot = _heldSlot;
            _heldSlot = null;
            _longPressFired = false;
            _tapPending = false;
            _longPressPending = false;
        }
    }
}
