using System;
using Client.Game.InGame.UI.ProgressBar;

namespace Client.Game.InGame.UI.UIState.State.DragDelete.Confirm
{
    /// <summary>
    ///     F長押しによる削除確定の進捗。対象の件数で決まる時間で満了し、採掘と同じ進捗バーへ流す
    ///     Hold-to-confirm progress for deletion: completes after a duration set by the subject's count, shown on the mining progress bar
    /// </summary>
    public class DeleteHoldConfirm
    {
        private const int MediumTierMinCount = 5;
        private const int LargeTierMinCount = 20;
        private const float SmallTierSeconds = 0.75f;
        private const float MediumTierSeconds = 1.25f;
        private const float LargeTierSeconds = 1.75f;

        private static float SecondsFor(int targetCount)
        {
            // 対象なしの長押しは進捗を始めないので0件はここに来ない
            // A hold without targets never starts progress, so zero never reaches here
            if (targetCount < 1) throw new ArgumentOutOfRangeException(nameof(targetCount), targetCount, "hold duration needs at least one target");
            if (LargeTierMinCount <= targetCount) return LargeTierSeconds;
            if (MediumTierMinCount <= targetCount) return MediumTierSeconds;
            return SmallTierSeconds;
        }

        private readonly ProgressBarState _progressBar;
        private DeleteHoldSubject _subject = DeleteHoldSubject.None;
        private float _heldSeconds;
        private bool _awaitRelease;

        private bool _isHolding;

        public DeleteHoldConfirm(ProgressBarState progressBar)
        {
            _progressBar = progressBar;
        }

        // 毎フレーム呼ぶ。満了したフレームだけ true。離す・対象が変わる・対象なしで進捗は0へ戻る
        // Call every frame; true only on the frame the hold completes. Release, a changed subject, or no subject resets progress
        public bool Advance(bool isKeyHeld, float deltaTime)
        {
            // 満了直後に押し続けても次の対象へ連続確定しない
            // Keeping the key down after completion must not chain into the next subject
            if (!isKeyHeld) _awaitRelease = false;
            if (!isKeyHeld || _subject.IsNone || _awaitRelease)
            {
                Reset();
                return false;
            }

            if (!_isHolding) Begin();

            var holdSeconds = SecondsFor(_subject.TargetCount);
            _heldSeconds += deltaTime;
            _progressBar.SetProgress(_heldSeconds / holdSeconds);
            if (_heldSeconds < holdSeconds) return false;

            Reset();
            _awaitRelease = true;
            return true;

            #region Internal

            void Begin()
            {
                if (!_isHolding) _progressBar.Show();
                _isHolding = true;
                _heldSeconds = 0f;
            }

            #endregion
        }

        public void SetSubject(DeleteHoldSubject subject)
        {
            if (_subject.Equals(subject)) return; // silent-ok: 同一対象への照準更新 / 進捗表示を保持
            Reset();
            _subject = subject;
        }

        // 取りやめた押下から別の対象への確定を連鎖させない
        // A cancelled key hold must not chain into confirmation of another target
        public void Cancel(bool isKeyHeld)
        {
            Reset();
            _awaitRelease = isKeyHeld;
        }

        public void Reset()
        {
            if (_isHolding) _progressBar.Hide();
            if (0f < _heldSeconds) _progressBar.SetProgress(0f);
            _isHolding = false;
            _heldSeconds = 0f;
        }

    }
}
