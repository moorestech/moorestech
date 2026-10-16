namespace Client.Input
{
    /// <summary>
    /// 走りの押下列から走り状態を解決する。トグル/ホールドの判定はここだけに置く
    /// Resolves the sprint state from sprint key events; the toggle/hold rule lives only here
    /// </summary>
    public class SprintInputResolver
    {
        public bool IsSprinting { get; private set; }

        private SprintInputMode _mode;
        private bool _isHeld;
        private bool _isCleanTap;
        private bool _isToggleAllowed = true;

        public SprintInputResolver(SprintInputMode mode)
        {
            _mode = mode;
            IsSprinting = mode == SprintInputMode.Toggle;
        }

        // トグルへ入る瞬間は開始時と同じく走りから始める
        // Entering toggle starts sprinting, the same as at game start
        public void SetMode(SprintInputMode mode)
        {
            _mode = mode;
            _isCleanTap = false;
            IsSprinting = mode == SprintInputMode.Toggle || _isHeld;
        }

        public void SetToggleAllowed(bool allowed)
        {
            _isToggleAllowed = allowed;
        }

        public void OnSprintPressed()
        {
            _isHeld = true;
            _isCleanTap = true;
            if (_mode == SprintInputMode.Hold) IsSprinting = true;
        }

        // 押下中の他ボタンはShiftを修飾キーとして使った印
        // Another button during the press marks Shift as used as a modifier
        public void OnOtherButtonPressed()
        {
            _isCleanTap = false;
        }

        public void OnSprintReleased()
        {
            _isHeld = false;
            if (_mode == SprintInputMode.Hold)
            {
                IsSprinting = false;
                return;
            }

            // 単独タップかつ抑止外のときだけ反転する
            // Flip only for a clean tap outside suppression
            if (_isCleanTap && _isToggleAllowed) IsSprinting = !IsSprinting;
            _isCleanTap = false;
        }

        // 入力無効の間は押下を見ていないため、実値へ揃えてタップ判定を捨てる
        // Presses are unseen while input is disabled, so resync to the held value and drop the tap
        public void ResyncHeld(bool isHeld)
        {
            _isHeld = isHeld;
            _isCleanTap = false;
            if (_mode == SprintInputMode.Hold) IsSprinting = isHeld;
        }
    }
}
