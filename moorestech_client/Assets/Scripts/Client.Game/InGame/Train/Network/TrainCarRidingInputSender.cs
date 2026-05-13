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