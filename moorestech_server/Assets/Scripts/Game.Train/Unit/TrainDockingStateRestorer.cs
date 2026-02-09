using System.Linq;

namespace Game.Train.Unit
{
    //事実上ゲーム起動時に一度だけ呼ばれる
    public class TrainDockingStateRestorer
    {
        private readonly TrainUpdateService _trainUpdateService;

        public TrainDockingStateRestorer(TrainUpdateService trainUpdateService)
        {
            _trainUpdateService = trainUpdateService;
        }

        public void RestoreDockingState()
        {
            var trains = _trainUpdateService.GetRegisteredTrains().ToArray();
            
            foreach (var train in trains)
            {
                train.trainUnitStationDocking.ClearDockingReceivers();
            }

            foreach (var train in trains)
            {
                train.trainUnitStationDocking.RestoreDockingFromSavedState();
            }
        }
    }
}
