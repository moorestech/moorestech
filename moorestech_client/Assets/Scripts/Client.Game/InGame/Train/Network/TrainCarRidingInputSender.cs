using Client.Game.InGame.Context;
using Client.Game.InGame.Train.Unit;
using Core.Update;
using UnityEngine;
using UnityEngine.InputSystem;
using VContainer.Unity;

namespace Client.Game.InGame.Train.Network
{
    public sealed class TrainCarRidingInputSender : ITickable
    {
        private const double TickSeconds = 1d / GameUpdater.TicksPerSecond;

        private readonly TrainCarRidingState _trainCarRidingState;
        private readonly TrainUnitClientCache _trainUnitClientCache;
        private double _elapsedSeconds;

        public TrainCarRidingInputSender(TrainCarRidingState trainCarRidingState, TrainUnitClientCache trainUnitClientCache)
        {
            _trainCarRidingState = trainCarRidingState;
            _trainUnitClientCache = trainUnitClientCache;
        }

        public void Tick()
        {
            _elapsedSeconds += Time.deltaTime;
            while (_elapsedSeconds >= TickSeconds)
            {
                _elapsedSeconds -= TickSeconds;
                SendIfRiding();
            }
        }

        private void SendIfRiding()
        {
            var ridingTrainCarInstanceId = _trainCarRidingState.CurrentRidingTrainCarInstanceId;
            if (!ridingTrainCarInstanceId.HasValue)
            {
                return;
            }
            if (!_trainUnitClientCache.TryGetCarSnapshot(ridingTrainCarInstanceId.Value, out _, out _, out _, out _))
            {
                _trainCarRidingState.ClearRidingTrainCar();
                return;
            }

            var keyboard = Keyboard.current;
            ClientContext.VanillaApi.SendOnly.SendTrainCarRidingInput(
                ridingTrainCarInstanceId.Value,
                keyboard?.wKey.isPressed ?? false,
                keyboard?.aKey.isPressed ?? false,
                keyboard?.sKey.isPressed ?? false,
                keyboard?.dKey.isPressed ?? false);
        }
    }
}
