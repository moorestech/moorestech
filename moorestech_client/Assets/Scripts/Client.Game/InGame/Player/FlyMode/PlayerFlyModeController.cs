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
        private const int EnterTapCount = 4;
        private const int ExitTapCount = 2;

        public bool IsFlying { get; private set; }

        private readonly ThirdPersonController _controller;
        private readonly SpaceTapSequence _tapSequence = new();

        public PlayerFlyModeController(ThirdPersonController controller)
        {
            _controller = controller;
        }

        public void ManualUpdate(bool isControllable, float unscaledTime)
        {
            // 操作不可の間は連打を数えず、途中の連打も捨てる
            // While uncontrollable, ignore taps and discard any partial sequence
            if (!isControllable)
            {
                _tapSequence.Reset();
                return;
            }

            if (InputManager.Player.Jump.GetKeyDown) HandleSpaceTap();
            if (IsFlying) _controller.SetFlightVerticalInput(ReadVerticalInput());

            #region Internal

            void HandleSpaceTap()
            {
                // 飛行中は2連打で解除、通常時は4連打で発動
                // Two taps leave while flying, four taps enter otherwise
                var tapCount = _tapSequence.RegisterTap(unscaledTime);
                if (IsFlying && tapCount >= ExitTapCount) SetFlying(false);
                else if (!IsFlying && tapCount >= EnterTapCount) TryEnter();
            }

            void TryEnter()
            {
                // トグルオフは無視するが、理由をログへ残す
                // Ignore when the toggle is off, but log why
                if (!DebugParameters.GetValueOrDefaultBool(DebugConst.FlyModeKey))
                {
                    _tapSequence.Reset();
                    Debug.Log($"[FlyMode] Space x{EnterTapCount} ignored: debug sheet toggle '{DebugConst.FlyModeLabel}' is off");
                    return;
                }
                SetFlying(true);
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

        private void SetFlying(bool isFlying)
        {
            // 入退のたびに連打を数え直し、ログに残す
            // Restart the tap count on every switch and log it
            IsFlying = isFlying;
            _tapSequence.Reset();
            _controller.SetFlying(isFlying);
            Debug.Log(isFlying ? "[FlyMode] Entered fly mode" : "[FlyMode] Left fly mode");
        }
    }
}
