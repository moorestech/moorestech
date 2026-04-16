using Game.Train.Unit;

namespace Client.Game.InGame.Presenter.Player
{
    public sealed class TrainManualOperationState
    {
        // 現在の手動運転対象だけを最小状態として保持する
        // Keep only the current manual-operation target as minimal state
        public bool IsOperating => _isOperating;
        public TrainInstanceId TargetTrainId => _targetTrainId;

        private bool _isOperating;
        private TrainInstanceId _targetTrainId = TrainInstanceId.Empty;

        public void StartOperating(TrainInstanceId targetTrainId)
        {
            // 無効な列車IDは停止扱いにして中立状態へ戻す
            // Treat an invalid train id as stop and return to neutral state
            if (targetTrainId == TrainInstanceId.Empty)
            {
                StopOperating();
                return;
            }

            // 操作開始時は対象列車IDを丸ごと差し替える
            // Replace the target train id when manual operation starts
            _isOperating = true;
            _targetTrainId = targetTrainId;
        }

        public void StopOperating()
        {
            // 停止時は対象列車も空にして送信側のcleanupを促す
            // Clear the target train on stop so the sender can clean up
            _isOperating = false;
            _targetTrainId = TrainInstanceId.Empty;
        }
    }
}
