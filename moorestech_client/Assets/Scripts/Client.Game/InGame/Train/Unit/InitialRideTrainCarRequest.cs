using Game.Train.Unit;

namespace Client.Game.InGame.Train.Unit
{
    public class InitialRideTrainCarRequest
    {
        public TrainCarInstanceId TargetCarId { get; }
        public int SeatIndex { get; }
        
        public InitialRideTrainCarRequest(TrainCarInstanceId targetCarId, int seatIndex)
        {
            TargetCarId = targetCarId;
            SeatIndex = seatIndex;
        }

    }
}