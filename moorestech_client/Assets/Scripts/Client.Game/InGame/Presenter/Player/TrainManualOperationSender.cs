using Client.Common.Server;
using Client.Game.InGame.Context;
using Client.Input;
using Game.Train.Unit;
using UnityEngine;
using VContainer.Unity;

namespace Client.Game.InGame.Presenter.Player
{
    public sealed class TrainManualOperationSender : ITickable
    {
        // 手動運転状態を見て入力送信の要否を決める
        // Decide whether input should be sent from manual operation state
        private readonly TrainManualOperationState _trainManualOperationState;

        private float _timer;
        private bool _wasOperating;
        private TrainInstanceId _lastOperatingTrainId = TrainInstanceId.Empty;

        public TrainManualOperationSender(TrainManualOperationState trainManualOperationState)
        {
            _trainManualOperationState = trainManualOperationState;
        }

        public void Tick()
        {
            // 手動運転中かつ有効な列車IDがあるときだけ送信処理を進める
            // Proceed with sending only while manual operation is active for a valid train
            if (!_trainManualOperationState.IsOperating || _trainManualOperationState.TargetTrainId == TrainInstanceId.Empty)
            {
                FlushNeutralInputIfNeeded();
                return;
            }

            // 操作対象が切り替わった瞬間は前の列車へ中立入力を1回送る
            // Send one neutral input to the previous train when the target changes
            if (_wasOperating && _lastOperatingTrainId != TrainInstanceId.Empty && _lastOperatingTrainId != _trainManualOperationState.TargetTrainId)
            {
                ClientContext.VanillaApi.SendOnly.SendTrainManualInput(_lastOperatingTrainId, TrainManualRawInputState.Neutral);
            }

            // 操作開始直後と対象切替直後は即時送信し、それ以外は通常周期で送る
            // Send immediately on start or target switch, otherwise on the normal cadence
            _timer += Time.deltaTime;
            var shouldSendImmediately = !_wasOperating || _lastOperatingTrainId != _trainManualOperationState.TargetTrainId;
            if (!shouldSendImmediately && _timer < NetworkConst.UpdateIntervalSeconds) return;

            // ここで現在の生入力を対象列車へ送って最新状態を上書きする
            // Send the current raw input to the target train and overwrite the latest state
            _timer = 0;
            var rawInput = ReadRawInput();
            ClientContext.VanillaApi.SendOnly.SendTrainManualInput(_trainManualOperationState.TargetTrainId, rawInput);
            _wasOperating = true;
            _lastOperatingTrainId = _trainManualOperationState.TargetTrainId;
        }

        private void FlushNeutralInputIfNeeded()
        {
            // 手動運転を抜けた瞬間だけ中立入力を1回送って押しっぱなし状態を消す
            // Send one neutral input on stop so the server can clear held input intent
            if (_wasOperating && _lastOperatingTrainId != TrainInstanceId.Empty)
            {
                ClientContext.VanillaApi.SendOnly.SendTrainManualInput(_lastOperatingTrainId, TrainManualRawInputState.Neutral);
            }

            _timer = 0;
            _wasOperating = false;
            _lastOperatingTrainId = TrainInstanceId.Empty;
        }

        private static TrainManualRawInputState ReadRawInput()
        {
            // プレイヤー移動入力をそのまま列車の生入力へ写す
            // Map player movement input directly into train raw input
            var move = InputManager.Player.Move.ReadValue<Vector2>();
            const float threshold = 0.5f;

            // WASD相当の軸入力を前後左右フラグへそのまま変換する
            // Convert the movement axis directly into forward, backward, left, and right flags
            return new TrainManualRawInputState(
                move.y > threshold,
                move.y < -threshold,
                move.x < -threshold,
                move.x > threshold);
        }
    }
}
