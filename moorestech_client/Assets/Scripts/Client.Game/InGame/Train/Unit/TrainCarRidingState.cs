using Game.Train.Unit;

namespace Client.Game.InGame.Train.Unit
{
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
