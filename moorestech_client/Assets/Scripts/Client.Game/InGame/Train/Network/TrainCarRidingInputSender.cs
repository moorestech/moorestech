using Client.Game.InGame.Context;
using Client.Game.InGame.Train.Unit;
using UnityEngine;
using VContainer.Unity;

namespace Client.Game.InGame.Train.Network
{
    public sealed class TrainCarRidingInputSender : ITickable
    {
        private readonly TrainCarRidingState _trainCarRidingState;
        private readonly TrainUnitClientCache _trainUnitClientCache;

        public TrainCarRidingInputSender(TrainCarRidingState trainCarRidingState, TrainUnitClientCache trainUnitClientCache)
        {
            _trainCarRidingState = trainCarRidingState;
            _trainUnitClientCache = trainUnitClientCache;
        }

        public void Tick()
        {
            SendIfRiding();
        }

        private void SendIfRiding()
        {
            var ridingTrainCarInstanceId = _trainCarRidingState.CurrentRidingTrainCarInstanceId;
            if (!ridingTrainCarInstanceId.HasValue)
                return;

            // 楽観的乗車中（seat=-1, RPC 応答未到達）は WASD 送信および cache miss 処理を一切しない。
            // RPC 確定前に cache 一時欠落で ClearRidingTrainCar されるレースを防ぐ。
            // Skip both WASD send and cache-miss handling while the seat is unconfirmed (seat=-1, awaiting RPC).
            // Prevents a race where a transient cache miss clears the ride before the RPC has resolved.
            if (_trainCarRidingState.CurrentSeatIndex < 0) return;

            // 対象車両が cache から消えたら強制降車する。
            // Force dismount if the target car disappeared from the cache.
            if (!_trainUnitClientCache.TryGetCarSnapshot(ridingTrainCarInstanceId.Value, out _, out _, out _, out _))
            {
                _trainCarRidingState.ClearRidingTrainCar();
                return;
            }

            ClientContext.VanillaApi.SendOnly.SendTrainCarRidingInput(
                ridingTrainCarInstanceId.Value,
                UnityEngine.Input.GetKey(KeyCode.W),
                UnityEngine.Input.GetKey(KeyCode.A),
                UnityEngine.Input.GetKey(KeyCode.S),
                UnityEngine.Input.GetKey(KeyCode.D));
        }
    }
}