using Client.Game.InGame.Control;
using Client.Input;

namespace Client.Game.InGame.UI.UIState.State.CancelInput
{
    /// <summary>
    ///     パネル外の右短押しを各UIStateがEsc判定の隣で問い合わせる唯一の入口。入力読取というUnity依存だけをここで解決する
    ///     The single entry point each UIState queries beside its Esc check; only the Unity-dependent input read lives here
    /// </summary>
    public class RightShortPressInputService
    {
        private readonly RightShortPressInput _rightShortPressInput;

        public RightShortPressInputService(RightShortPressInput rightShortPressInput)
        {
            _rightShortPressInput = rightShortPressInput;
        }

        // 毎フレーム呼ぶ。右短押しが成立したフレームだけtrue
        // Call every frame; true only on the frame a short press outside UI is confirmed
        public bool TryConsumeShortPressOutsideUi()
        {
            _rightShortPressInput.ManualUpdate(HybridInput.GetMouseButton(1), HybridInput.GetMouseDelta(), UiPointerHitTest.IsPointerOverAnyUi());
            return _rightShortPressInput.TryConsumeShortPress();
        }

        // UIState遷移のたびに押下を捨てる。他状態滞在中はpollされないため復帰直後の誤発火を防ぐ
        // Drops the held press on every UIState transition; nothing polls while another state is active, so this prevents a stale fire on return
        public void ResetPressState()
        {
            _rightShortPressInput.Reset(HybridInput.GetMouseButton(1));
        }
    }
}
