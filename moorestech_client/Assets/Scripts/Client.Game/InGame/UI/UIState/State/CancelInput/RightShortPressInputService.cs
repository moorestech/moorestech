using Client.Game.InGame.Control;
using Client.Input;
using UnityEngine;

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

            // フォーカス喪失でInputSystemがマウスを離し扱いに戻すため、押したままのAlt+Tabが短押しに化ける前にここで押下を捨てる
            // Focus loss makes the InputSystem report the mouse as released, so a held press is discarded here before Alt+Tab turns it into a short press
            Application.focusChanged += OnApplicationFocusChanged;
        }

        // 毎フレーム呼ぶ。右短押しが成立したフレームだけtrue
        // Call every frame; true only on the frame a short press outside UI is confirmed
        public bool TryConsumeShortPressOutsideUi()
        {
            var isDeltaMeasured = HybridInput.TryGetMouseDelta(out var pointerDelta);
            _rightShortPressInput.ManualUpdate(HybridInput.GetMouseButton(1), isDeltaMeasured, pointerDelta, UiPointerHitTest.IsPointerOverAnyUi());
            return _rightShortPressInput.TryConsumeShortPress();
        }

        // UIState遷移のたびに押下を捨てる。他状態滞在中はpollされないため復帰直後の誤発火を防ぐ
        // Drops the held press on every UIState transition; nothing polls while another state is active, so this prevents a stale fire on return
        public void ResetPressState()
        {
            _rightShortPressInput.Reset(HybridInput.GetMouseButton(1));
        }

        private void OnApplicationFocusChanged(bool hasFocus)
        {
            if (hasFocus) return;

            // 喪失時点の物理押下はデバイスリセット後で読めないため、押下中とみなして死に押下にする
            // The physical state is unreadable after the device reset, so treat the press as held and mark it dead
            _rightShortPressInput.Reset(true);
        }
    }
}
