using Game.Train.Unit;

namespace Client.Game.InGame.Train.Unit
{
    // GameScreenState の入力検出時に、TrainHUDScreenState へ渡される乗車要求コンテキスト。
    // UITransitContextContainer に詰めて遷移先 (TrainHUDScreenState.OnEnter) で取り出す。
    // Ride request context passed from GameScreenState input detection to TrainHUDScreenState.
    // Carried via UITransitContextContainer and retrieved in TrainHUDScreenState.OnEnter.
    public sealed class RideVehicleRequest
    {
        public TrainCarInstanceId TargetCarId { get; }

        public RideVehicleRequest(TrainCarInstanceId targetCarId)
        {
            TargetCarId = targetCarId;
        }
    }
}
