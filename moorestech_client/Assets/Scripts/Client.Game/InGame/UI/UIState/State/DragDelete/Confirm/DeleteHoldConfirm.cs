using Client.Game.InGame.UI.ProgressBar;

namespace Client.Game.InGame.UI.UIState.State.DragDelete.Confirm
{
    /// <summary>
    ///     F長押しによる削除確定の進捗。件数に関係なく一定時間で満了し、採掘と同じ進捗バーへ流す
    ///     Hold-to-confirm progress for deletion: a fixed duration regardless of count, shown on the mining progress bar
    /// </summary>
    public class DeleteHoldConfirm
    {
        public const float HoldSeconds = 2f;

        private readonly ProgressBarState _progressBar;
        private DeleteHoldSubject _subject = DeleteHoldSubject.None;
        private float _heldSeconds;
        private bool _awaitRelease;

        public bool IsHolding { get; private set; }

        public DeleteHoldConfirm(ProgressBarState progressBar)
        {
            _progressBar = progressBar;
        }

        // 毎フレーム呼ぶ。満了したフレームだけ true。離す・対象が変わる・対象なしで進捗は0へ戻る
        // Call every frame; true only on the frame the hold completes. Release, a changed subject, or no subject resets progress
        public bool Advance(bool isKeyHeld, DeleteHoldSubject subject, float deltaTime)
        {
            // 満了直後に押し続けても次の対象へ連続確定しない
            // Keeping the key down after completion must not chain into the next subject
            if (!isKeyHeld) _awaitRelease = false;
            if (!isKeyHeld || subject.IsNone || _awaitRelease)
            {
                Reset();
                return false;
            }

            if (!IsHolding || !_subject.Equals(subject)) Begin(subject);

            _heldSeconds += deltaTime;
            _progressBar.SetProgress(_heldSeconds / HoldSeconds);
            if (_heldSeconds < HoldSeconds) return false;

            Reset();
            _awaitRelease = true;
            return true;
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
            if (IsHolding) _progressBar.Hide();
            if (_heldSeconds > 0f) _progressBar.SetProgress(0f);
            IsHolding = false;
            _subject = DeleteHoldSubject.None;
            _heldSeconds = 0f;
        }

        private void Begin(DeleteHoldSubject subject)
        {
            if (!IsHolding) _progressBar.Show();
            IsHolding = true;
            _subject = subject;
            _heldSeconds = 0f;
        }
    }
}
