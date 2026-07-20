using Client.Game.InGame.Context;
using Client.Input;
using UnityEngine;

namespace Client.Game.InGame.UI.UIState.State.TrainHUDScreen
{
    public sealed class TrainRidingInputSender
    {
        private const float TrainInputHeartbeatIntervalSeconds = 2f;

        private bool _hasSentTrainMoveInput;
        private bool _lastSentMoveForward;
        private bool _lastSentMoveBackward;
        private float _lastTrainInputSentAt;

        public void Reset()
        {
            _hasSentTrainMoveInput = false;
            _lastSentMoveForward = false;
            _lastSentMoveBackward = false;
            _lastTrainInputSentAt = 0f;
        }

        public void Update()
        {
            // 乗車中の操作意味でキー入力を読む。
            // Read keys as train-riding control intents.
            var moveForward = HybridInput.GetKey(KeyCode.W);
            var selectPreviousBranch = HybridInput.GetKeyDown(KeyCode.A);
            var moveBackward = HybridInput.GetKey(KeyCode.S);
            var selectNextBranch = HybridInput.GetKeyDown(KeyCode.D);

            // W/Sは状態差分と定期送信、A/Dは押し下げだけを送る。
            // Send W/S on state changes and heartbeat, while A/D is key-down only.
            var moveForwardChanged = !_hasSentTrainMoveInput || moveForward != _lastSentMoveForward;
            var moveBackwardChanged = !_hasSentTrainMoveInput || moveBackward != _lastSentMoveBackward;
            var isHeartbeatDue = _hasSentTrainMoveInput && Time.realtimeSinceStartup - _lastTrainInputSentAt >= TrainInputHeartbeatIntervalSeconds;
            var shouldSendInput = moveForwardChanged || moveBackwardChanged || isHeartbeatDue || selectPreviousBranch || selectNextBranch;
            if (!shouldSendInput) return;

            // サーバーへ現在の入力状態と分岐選択イベントを送る。
            // Send the current movement state and branch selection events to the server.
            ClientContext.VanillaApi.SendOnly.SendTrainCarRidingInput(
                moveForward,
                moveBackward,
                selectPreviousBranch,
                selectNextBranch);
            _hasSentTrainMoveInput = true;
            _lastSentMoveForward = moveForward;
            _lastSentMoveBackward = moveBackward;
            _lastTrainInputSentAt = Time.realtimeSinceStartup;
        }
    }
}
