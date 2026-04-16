namespace Game.Train.Unit
{
    //事実上ゲーム起動時に一度だけ呼ばれる
    public class TrainDockingStateRestorer
    {
        private ITrainUnitLookupDatastore _trainUnitLookupDatastore;
        public TrainDockingStateRestorer(ITrainUnitLookupDatastore trainUnitLookupDatastore)
        {
            _trainUnitLookupDatastore = trainUnitLookupDatastore;
        }

        public void RestoreDockingState()
        {
            var trains = _trainUnitLookupDatastore.GetRegisteredTrains(); 
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
