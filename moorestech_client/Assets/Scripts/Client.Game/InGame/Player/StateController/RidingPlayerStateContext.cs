using Game.Train.Unit;

namespace Client.Game.InGame.Player.StateController
{
    public class RidingPlayerStateContext : IPlayerStateContext
    {
        public TrainCarInstanceId? CurrentCarId { get; private set; }
        public int CurrentSeatIndex { get; private set; } = -1;
        
        public RidingPlayerStateContext(TrainCarInstanceId rideRequestTargetCarId, int seatIndex)
        {
            CurrentCarId = rideRequestTargetCarId;
            CurrentSeatIndex = seatIndex;
        }
    }
}
