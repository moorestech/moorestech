using Game.Block.Interface.Component;
using Game.World.Interface.DataStore;
using System.Linq;

namespace Game.Train.Common
{
    //事実上ゲーム起動時に一度だけ呼ばれる
    public class TrainDockingStateRestorer
    {
        private readonly TrainUpdateService _trainUpdateService;
        private readonly IWorldBlockDatastore _worldBlockDatastore;

        public TrainDockingStateRestorer(TrainUpdateService trainUpdateService, IWorldBlockDatastore worldBlockDatastore)
        {
            _trainUpdateService = trainUpdateService;
            _worldBlockDatastore = worldBlockDatastore;
        }

        public void RestoreDockingState()
        {
            var trains = _trainUpdateService.GetRegisteredTrains().ToArray();

            foreach (var train in trains)
            {
                train.trainUnitStationDocking.ClearDockingReceivers();
            }

            foreach (var blockData in _worldBlockDatastore.BlockMasterDictionary.Values)
            {
                foreach (var receiver in blockData.Block.ComponentManager.GetComponents<ITrainDockingReceiver>())
                {
                    receiver.ForceUndock();
                }
            }

            foreach (var train in trains)
            {
                train.trainUnitStationDocking.RestoreDockingFromSavedState();
            }
        }
    }
}
