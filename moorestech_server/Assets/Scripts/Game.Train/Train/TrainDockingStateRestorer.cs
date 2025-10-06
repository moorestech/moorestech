using System.Linq;
using Game.Block.Interface.Component;
using Game.Context;
using Game.Train.Common;

namespace Game.Train.Train
{
    //事実上ゲーム起動時に一度だけ呼ばれる
    public static class TrainDockingStateRestorer
    {
        public static void RestoreDockingState()
        {
            var trains = TrainUpdateService.Instance.GetRegisteredTrains().ToArray();
            var trainsToRedock = trains.Where(train => train.trainUnitStationDocking.IsDocked).ToArray();

            foreach (var train in trains)
            {
                train.trainUnitStationDocking.UndockFromStation();
            }

            var worldBlockDatastore = ServerContext.WorldBlockDatastore;
            foreach (var blockData in worldBlockDatastore.BlockMasterDictionary.Values)
            {
                foreach (var receiver in blockData.Block.ComponentManager.GetComponents<ITrainDockingReceiver>())
                {
                    receiver.ForceUndock();
                }
            }

            foreach (var train in trainsToRedock)
            {
                train.trainUnitStationDocking.TryDockWhenStopped();
            }
        }
    }
}
