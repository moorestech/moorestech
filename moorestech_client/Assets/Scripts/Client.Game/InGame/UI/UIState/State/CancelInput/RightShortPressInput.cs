using UnityEngine;

namespace Client.Game.InGame.UI.UIState.State.CancelInput
{
    /// <summary>
    ///     右ボタンの押下を「短押し（動かさず離す）」と「ドラッグ」に判別する状態機械。入力読取は呼び出し側がプッシュする
    ///     State machine classifying a right-button press as a short press (released without moving) or a drag; the caller pushes the input
    /// </summary>
    public class RightShortPressInput
    {
        public const float MoveThresholdPixels = 8f;

        private bool _isHeld;
        private Vector2 _pressStartPosition;

        // 押下がまだ短押し候補か。パネル上で押した・閾値以上動いた時点でfalseに落ちる
        // Whether the current press is still a short-press candidate; drops to false when pressed over UI or moved past the threshold
        private bool _isArmed;

        // Reset時に押下中だった押下。離されるまで再武装させない
        // A press that was held at Reset time; it must not re-arm until released
        private bool _isDeadPress;

        private bool _shortPressPending;

        // 押下継続を1フレーム進め、離した瞬間に短押しを確定する
        // Advances the press by one frame and confirms a short press at the moment of release
        public void ManualUpdate(bool isRightHeld, Vector2 pointerPosition, bool isPointerOverUi)
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
                if (!nextHeld && _isArmed) _shortPressPending = true;

                _isHeld = nextHeld;
                if (nextHeld) _pressStartPosition = pointerPosition;
                _isArmed = nextHeld && !isPointerOverUi;
            }

            // 閾値以上動いたらドラッグとみなし、この押下では二度と成立させない
            // Moving past the threshold makes it a drag; this press can never become a short press again
            void DisarmIfMoved()
            {
                if (!_isHeld || !_isArmed) return;
                if ((pointerPosition - _pressStartPosition).sqrMagnitude < MoveThresholdPixels * MoveThresholdPixels) return;

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
        public void Reset()
        {
            if (_isHeld) _isDeadPress = true;
            _isHeld = false;
            _isArmed = false;
            _shortPressPending = false;
        }
    }
}
