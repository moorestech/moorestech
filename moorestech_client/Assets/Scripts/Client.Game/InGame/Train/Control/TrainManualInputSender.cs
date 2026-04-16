using Client.DebugSystem;
using Client.Game.InGame.Context;
using Core.Update;
using Game.Train.Unit;
using UnityEngine;
using UnityEngine.InputSystem;
using VContainer.Unity;

namespace Client.Game.InGame.Train.Control
{
    public sealed class TrainManualInputSender : ITickable
    {
        private const float SendIntervalSeconds = (float)GameUpdater.SecondsPerTick;

        private float _timer;

        public void Tick()
        {
            if (!TrainManualDebugState.IsRiding || TrainManualDebugState.SelectedTrainCarId < 0)
            {
                _timer = 0f;
                return;
            }

            _timer += Time.deltaTime;
            if (_timer < SendIntervalSeconds)
            {
                return;
            }

            _timer = 0f;
            ClientContext.VanillaApi.SendOnly.SendTrainManualInput(
                TrainManualDebugState.SelectedTrainCarId,
                (int)ReadRawInputMask());
        }

        private static TrainManualInputFlags ReadRawInputMask()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return TrainManualInputFlags.None;
            }

            var inputFlags = TrainManualInputFlags.None;
            if (keyboard.wKey.isPressed)
            {
                inputFlags |= TrainManualInputFlags.Forward;
            }
            if (keyboard.aKey.isPressed)
            {
                inputFlags |= TrainManualInputFlags.Left;
            }
            if (keyboard.sKey.isPressed)
            {
                inputFlags |= TrainManualInputFlags.Backward;
            }
            if (keyboard.dKey.isPressed)
            {
                inputFlags |= TrainManualInputFlags.Right;
            }

            return inputFlags;
        }
    }
}
