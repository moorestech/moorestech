using Game.Train.Unit;

namespace Client.Game.InGame.Train.Unit
{
    // UIステートマシン上で乗車するTrainを管理するためのクラス
    // A class for managing the Train to ride on the UI state machine.
    public sealed class RideTrainCarRequest
    {
        public TrainCarInstanceId TargetCarId { get; }

        public RideTrainCarRequest(TrainCarInstanceId targetCarId)
        {
            TargetCarId = targetCarId;
        }
    }
}
