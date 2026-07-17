using System;
using Game.Context;
using Game.PlayerRiding.Interface;
using Game.Train.Event;
using Game.Train.Unit;
using UniRx;

namespace Game.PlayerRiding
{
    // 車両削除通知を乗車状態の降車処理へ接続する。
    // Connects train-car removal notifications to riding-state dismount handling.
    public class RemovedRidableRidingHandler : IDisposable, IBootInitializable
    {
        private readonly IPlayerRidingDatastore _playerRidingDatastore;
        private readonly IDisposable _trainCarRemovedSubscription;

        public RemovedRidableRidingHandler(ITrainUpdateEvent trainUpdateEvent, IPlayerRidingDatastore playerRidingDatastore)
        {
            _playerRidingDatastore = playerRidingDatastore;
            _trainCarRemovedSubscription = trainUpdateEvent.OnTrainCarRemoved.Subscribe(OnTrainCarRemoved);
        }

        public void Dispose()
        {
            _trainCarRemovedSubscription.Dispose();
        }

        private void OnTrainCarRemoved(TrainCarInstanceId trainCarInstanceId)
        {
            // 削除された車両の乗員を接続中プレイヤーに限って降車させる。
            // Dismount connected riders of the removed train car.
            var identifier = new TrainCarRidableIdentifier(trainCarInstanceId.AsPrimitive());
            _playerRidingDatastore.OnRidableRemoved(identifier);
        }
    }
}
