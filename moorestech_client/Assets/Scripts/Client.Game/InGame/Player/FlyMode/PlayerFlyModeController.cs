using Client.Input;
using Common.Debug;
using StarterAssets;
using UnityEngine;

namespace Client.Game.InGame.Player.FlyMode
{
    // デバッグ用フライモードの入退と上下入力をThirdPersonControllerへ渡す
    // Drives entering/leaving the debug fly mode and pushes vertical input to ThirdPersonController
    public class PlayerFlyModeController
    {
        private readonly ThirdPersonController _controller;
        private readonly SpaceTapSequence _tapSequence = new();
        private bool _isControllable = true;

        public PlayerFlyModeController(ThirdPersonController controller)
        {
            _controller = controller;
        }

        public void SetControllable(bool isControllable)
        {
            _isControllable = isControllable;
            if (isControllable) return;

            // 操作不可へ移る瞬間に途中の連打と上下入力を捨て、復帰時に持ち越さない
            // Drop the partial taps and vertical input when control is lost so neither survives the return
            _tapSequence.Reset();
            _controller.SetFlightVerticalInput(0f);
        }

        public void ManualUpdate(float unscaledTime)
        {
            // 操作不可の間は連打を数えず、押下だけ理由をログへ残す
            // While uncontrollable, ignore taps and log the reason only on the press frame
            if (!_isControllable)
            {
                if (InputManager.Player.Jump.GetKeyDown) Debug.Log("[FlyMode] Space ignored: player is not controllable (movement lock or riding)");
                return;
            }

            if (InputManager.Player.Jump.GetKeyDown) HandleSpaceTap();
            if (_controller.IsFlying()) _controller.SetFlightVerticalInput(ReadVerticalInput());

            #region Internal

            void HandleSpaceTap()
            {
                // 飛行中は2連打で解除、通常時は4連打で発動
                // Two taps leave while flying, four taps enter otherwise
                var tapCount = _tapSequence.RegisterTap(unscaledTime);
                var isFlying = _controller.IsFlying();
                if (isFlying && DebugConst.FlyModeExitTapCount <= tapCount) SetFlying(false);
                else if (!isFlying && DebugConst.FlyModeEnterTapCount <= tapCount) TryEnter();
            }

            void TryEnter()
            {
                // トグルオフは無視するが、理由をログへ残す
                // Ignore when the toggle is off, but log why
                if (!DebugParameters.GetValueOrDefaultBool(DebugConst.FlyModeKey))
                {
                    _tapSequence.Reset();
                    Debug.Log($"[FlyMode] Space x{DebugConst.FlyModeEnterTapCount} ignored: debug sheet toggle '{DebugConst.FlyModeLabel}' is off");
                    return;
                }
                SetFlying(true);
            }

            void SetFlying(bool isFlying)
            {
                // 入退のたびに連打を数え直し、ログに残す
                // Restart the tap count on every switch and log it
                _tapSequence.Reset();
                _controller.SetFlying(isFlying);
                Debug.Log(isFlying ? "[FlyMode] Entered fly mode" : "[FlyMode] Left fly mode");
            }

            float ReadVerticalInput()
            {
                // EとQの同時押しは相殺する
                // Holding both E and Q cancels out
                var vertical = 0f;
                if (HybridInput.GetKey(KeyCode.E)) vertical += 1f;
                if (HybridInput.GetKey(KeyCode.Q)) vertical -= 1f;
                return vertical;
            }

            #endregion
        }
    }
}
