using UniRx;
using Core.Update;
using Game.Train.Train;
using System.Collections.Generic;


namespace Game.Train.Common
{
    public class TrainUpdateService
    {
        private readonly List<TrainUnit> _trainUnits = new();

        public TrainUpdateService()
        {
            GameUpdater.UpdateObservable
                .Subscribe(_ => UpdateTrains());
        }

        private void UpdateTrains()
        {
            foreach (var trainUnit in _trainUnits)
            {
                trainUnit.UpdateTrain(GameUpdater.UpdateSecondTime);
            }
        }

        public void RegisterTrain(TrainUnit trainUnit) => _trainUnits.Add(trainUnit);
        public void UnregisterTrain(TrainUnit trainUnit) => _trainUnits.Remove(trainUnit);
    }
}
