using Game.Train.Unit;

namespace Client.Game.InGame.Train.Unit
{
    // 乗車中の TrainCarInstanceId と座席インデックスを保持する。
    // Holds the current riding TrainCarInstanceId and seat index.
    public sealed class TrainCarRidingState
    {
        public bool IsRiding => CurrentRidingTrainCarInstanceId.HasValue;
        public TrainCarInstanceId? CurrentRidingTrainCarInstanceId { get; private set; }
        public int CurrentSeatIndex { get; private set; } = -1;

        public void SetRidingTrainCar(TrainCarInstanceId trainCarInstanceId, int seatIndex)
        {
            CurrentRidingTrainCarInstanceId = trainCarInstanceId;
            CurrentSeatIndex = seatIndex;
        }

        public void ClearRidingTrainCar()
        {
            CurrentRidingTrainCarInstanceId = null;
            CurrentSeatIndex = -1;
        }
    }
}
