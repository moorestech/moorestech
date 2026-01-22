using System.Collections.Generic;
using UnityEngine;
using VContainer.Unity;

namespace Client.Game.InGame.Train.Unit
{
    public sealed class TrainUnitClientSimulator : ITickable
    {
        private const double TickSeconds = 1d / 60d;
        private readonly TrainUnitClientCache _cache;
        private readonly List<ClientTrainUnit> _work = new();
        private double _accumulatedSeconds;

        public TrainUnitClientSimulator(TrainUnitClientCache cache)
        {
            _cache = cache;
        }

        public void Tick()
        {
            _accumulatedSeconds += Time.deltaTime;
            var tickCount = (int)(_accumulatedSeconds / TickSeconds);
            if (tickCount <= 0)
            {
                return;
            }

            _accumulatedSeconds -= tickCount * TickSeconds;
            for (var i = 0; i < tickCount; i++)
            {
                SimulateOneTick();
            }
        }

        private void SimulateOneTick()
        {
            _cache.CopyUnitsTo(_work);
            for (var i = 0; i < _work.Count; i++)
            {
                _work[i].Update();
            }
        }
    }
}
