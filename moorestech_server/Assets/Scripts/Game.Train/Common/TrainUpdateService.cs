using UniRx;
using Core.Update;
using Game.Train.Train;
using System.Collections.Generic;
using System.Linq;


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


        private readonly List<TrainUnit> _trainUnits = new();

        public TrainUpdateService()
        {
            GameUpdater.UpdateObservable
                .Subscribe(_ => UpdateTrains());
        }

        private void UpdateTrains()
        {
            var deltaTime = GameUpdater.UpdateSecondTime;
            foreach (var trainUnit in _trainUnits)
            {
                trainUnit.Update(deltaTime);
            }
        }

        public void RegisterTrain(TrainUnit trainUnit) => _trainUnits.Add(trainUnit);
        public void UnregisterTrain(TrainUnit trainUnit) => _trainUnits.Remove(trainUnit);
        public IEnumerable<TrainUnit> GetRegisteredTrains() => _trainUnits.ToArray();
    }
}
