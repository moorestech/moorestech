using UnityEngine;

namespace Client.Game.InGame.UI.UIState.State.CancelInput
{
    /// <summary>
    ///     右ボタンの押下を「短押し（動かさず離す）」と「ドラッグ」に判別する状態機械。入力読取は呼び出し側がプッシュする
    ///     State machine classifying a right-button press as a short press (released without moving) or a drag; the caller pushes the input
    ///
    ///     移動量は絶対座標でなくフレーム毎のdeltaを累積する。TPS右押下でカーソルがロックされ座標が凍結するため。
    ///     Movement accumulates per-frame deltas instead of absolute positions, because the TPS right press locks the cursor and freezes its position.
    /// </summary>
    public class RightShortPressInput
    {
        private const float MoveThresholdPixels = 8f;

        private bool _isHeld;

        // 押下開始からの移動軌跡長。往復して戻っても減らない
        // Path length travelled since the press started; it never shrinks when the pointer returns
        private float _movedDistance;

        // 押下がまだ短押し候補か。パネル上で押した・閾値以上動いた時点でfalseに落ちる
        // Whether the current press is still a short-press candidate; drops to false when pressed over UI or moved past the threshold
        private bool _isArmed;

        // Reset時に押下中だった押下。離されるまで再武装させない
        // A press that was held at Reset time; it must not re-arm until released
        private bool _isDeadPress;

        private bool _shortPressPending;

        // 押下継続を1フレーム進め、離した瞬間に短押しを確定する
        // Advances the press by one frame and confirms a short press at the moment of release
        public void ManualUpdate(bool isRightHeld, Vector2 pointerDelta, bool isPointerOverUi)
        {
            if (!isRightHeld) _isDeadPress = false;
            var isActiveHeld = isRightHeld && !_isDeadPress;

            if (isActiveHeld != _isHeld)
            {
                HandleHeldChanged(isActiveHeld);
                return;
            }

            DisarmIfMoved();

            #region Internal

            // 押下開始で武装（パネル上なら非武装）、離しで武装中のみ確定
            // Arms on press start (unless over UI) and confirms on release only while still armed
            void HandleHeldChanged(bool nextHeld)
            {
                // 離しフレームの移動も確定前に加算する。高速ドラッグの移動が離しフレームに集中しても閾値判定から漏れない
                // Accumulate the release frame's movement before confirming, so a fast drag whose movement lands on the release frame is still caught
                if (!nextHeld)
                {
                    _movedDistance += pointerDelta.magnitude;
                    if (MoveThresholdPixels <= _movedDistance) _isArmed = false;

                    // 離した位置がパネル上なら成立させない。開始・終了のどちらかがパネル上ならUI操作として扱う
                    // A release over a panel never confirms: a press whose start or end is over UI counts as a UI operation
                    if (isPointerOverUi) _isArmed = false;

                    if (_isArmed) _shortPressPending = true;
                }

                _isHeld = nextHeld;
                _isArmed = nextHeld && !isPointerOverUi;

                // 押下フレームの移動も離しフレームと対称に数える。押しながら振った高速フリックを短押しにしない
                // The press frame's movement counts too, symmetric with release, so a fast flick started mid-motion is not a short press
                if (!nextHeld) return;
                _movedDistance = pointerDelta.magnitude;
                if (MoveThresholdPixels <= _movedDistance) _isArmed = false;
            }

            // 累積移動が閾値に達したらドラッグとみなし、この押下では二度と成立させない
            // Once the accumulated movement reaches the threshold it is a drag; this press can never become a short press again
            void DisarmIfMoved()
            {
                if (!_isHeld || !_isArmed) return;

                _movedDistance += pointerDelta.magnitude;
                if (_movedDistance < MoveThresholdPixels) return;

                _isArmed = false;
            }

            #endregion
        }

        // 確定した短押しを1回だけ消費する
        // Consumes a confirmed short press exactly once
        public bool TryConsumeShortPress()
        {
            if (!_shortPressPending) return false;

            _shortPressPending = false;
            return true;
        }

        // 押下中の押下を消費済みにし、遷移直後の誤発火を防ぐ
        // Marks the held press consumed so a transition cannot produce a false short press
        //
        // isRightHeldNowは呼び出し時点の物理押下。pollされていない状態で始まった押下は_isHeldに現れないため外から渡す
        // isRightHeldNow is the physical button state at call time: a press started while nothing polled never shows up in _isHeld
        public void Reset(bool isRightHeldNow)
        {
            if (_isHeld || isRightHeldNow) _isDeadPress = true;
            _isHeld = false;
            _isArmed = false;
            _shortPressPending = false;
        }
    }
}
