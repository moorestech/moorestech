using Game.Train.Unit;

namespace Client.Game.InGame.Player.StateController
{
    // Riding ステート用 context。列車ステート (TrainHUDScreenState) が実装し、現在乗るべき列車情報を提供する。
    // RidingPlayerState はこの context 経由で対象車両と座席を毎フレーム照会する。
    // Context for the Riding state, implemented by the train state (TrainHUDScreenState).
    // RidingPlayerState queries this context every frame for the current car and seat.
    public interface IPlayerRideContext : IPlayerStateContext
    {
        bool TryGetCurrentRideTarget(out TrainCarInstanceId carId, out int seatIndex);
    }
}
