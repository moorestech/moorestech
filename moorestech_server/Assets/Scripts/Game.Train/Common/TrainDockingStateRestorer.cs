using Game.Block.Interface.Component;
using Game.World.Interface.DataStore;
using System.Linq;
using Game.Context;

namespace Game.Train.Common
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
            var _worldBlockDatastore = ServerContext.WorldBlockDatastore;
            
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
