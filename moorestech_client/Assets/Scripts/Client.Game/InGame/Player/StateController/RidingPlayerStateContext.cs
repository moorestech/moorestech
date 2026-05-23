using Game.Train.Unit;

namespace Client.Game.InGame.Player.StateController
{
    // Riding ステート用 context。列車ステート (TrainHUDScreenState) が 1 instance を持ち、
    // RidingPlayerState には同じインスタンスへの参照を渡す。状態（座席確定・強制降車）の更新は
    // 列車ステート側で context のメソッドを呼び、RidingPlayerState はプロパティを毎フレーム読む。
    // Context for the Riding state. TrainHUDScreenState owns one instance and hands a reference to RidingPlayerState.
    // The train state mutates the context via methods (seat confirmation, forced dismount) while RidingPlayerState
    // reads the properties every frame.
    public class RidingPlayerStateContext : IPlayerStateContext
    {
        // 現在乗車中の車両 id。null は「乗っていない」。
        // Current riding car id; null means "not riding".
        public TrainCarInstanceId? CurrentCarId { get; private set; }
        // 座席 index。-1 は「RPC 応答未到達 (楽観反映中)」。
        // Seat index; -1 means "RPC reply pending (optimistic)".
        public int CurrentSeatIndex { get; private set; } = -1;

        public void SetRideTarget(TrainCarInstanceId carId, int seatIndex)
        {
            CurrentCarId = carId;
            CurrentSeatIndex = seatIndex;
        }

        public void Clear()
        {
            CurrentCarId = null;
            CurrentSeatIndex = -1;
        }
    }
}
