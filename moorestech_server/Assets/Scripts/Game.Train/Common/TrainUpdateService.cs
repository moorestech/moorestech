using System;
using System.Collections.Generic;
using System.Linq;
using Core.Update;
using Game.Train.Train;
using UniRx;


namespace Game.Train.Common
{
    public class TrainUpdateService
    {
        private static TrainUpdateService _instance;
        public static TrainUpdateService Instance
        {
            get
            {
                if (_instance == null)
                    _instance = new TrainUpdateService();
                return _instance;
            }
        }

        //マジックナンバー。trainはtick制。1tickで速度、位置、ドッキング状態等が決定的に動く。1tick=1/120秒
        private const double TickSeconds = 1d / 120d;
        private double _accumulatedSeconds;
        private readonly int _maxTicksPerFrame = 65535;
        private readonly List<TrainUnit> _trainUnits = new();

        public TrainUpdateService()
        {
            GameUpdater.UpdateObservable
                .Subscribe(_ => UpdateTrains());
        }

        private void UpdateTrains()
        {
            _accumulatedSeconds += GameUpdater.UpdateSecondTime;

            var tickCount = Math.Min(_maxTicksPerFrame, (int)(_accumulatedSeconds / TickSeconds));
            if (tickCount == 0)
            {
                return;
            }

            _accumulatedSeconds -= tickCount * TickSeconds;

            for (var i = 0; i < tickCount; i++)
            {
                foreach (var trainUnit in _trainUnits)
                {
                    trainUnit.Update();
                }
            }
        }


        private void UpdateTrains1Tickmanually()
        {
            foreach (var trainUnit in _trainUnits)
            {
                trainUnit.Update();
            }
        }


        public void RegisterTrain(TrainUnit trainUnit) => _trainUnits.Add(trainUnit);
        public void UnregisterTrain(TrainUnit trainUnit) => _trainUnits.Remove(trainUnit);
        public IEnumerable<TrainUnit> GetRegisteredTrains() => _trainUnits.ToArray();
        public void ResetTrains()
        {
            _trainUnits.Clear();
            _accumulatedSeconds = 0d;
        }

#if UNITY_INCLUDE_TESTS
        public void ResetTickAccumulator()
        {
            _accumulatedSeconds = 0d;
        }
#endif
    }
}
