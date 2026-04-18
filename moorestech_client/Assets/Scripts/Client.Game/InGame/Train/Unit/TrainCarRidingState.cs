using Game.Train.Unit;

namespace Client.Game.InGame.Train.Unit
{
    // 乗車中の TrainCarInstanceId だけを保持する。
    // Holds only the current riding TrainCarInstanceId.
    public sealed class TrainCarRidingState
    {
        public bool IsRiding => CurrentRidingTrainCarInstanceId.HasValue;
        public TrainCarInstanceId? CurrentRidingTrainCarInstanceId { get; private set; }

        public void SetRidingTrainCar(TrainCarInstanceId trainCarInstanceId)
        {
            CurrentRidingTrainCarInstanceId = trainCarInstanceId;
        }

        public void ClearRidingTrainCar()
        {
            CurrentRidingTrainCarInstanceId = null;
        }
    }
}
