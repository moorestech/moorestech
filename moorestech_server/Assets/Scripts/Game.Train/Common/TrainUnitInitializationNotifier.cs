using System;
using Game.Train.Train;
using UniRx;

namespace Game.Train.Common
{
    public sealed class TrainUnitInitializationNotifier : IDisposable
    {
        private Subject<TrainUnitCreatedData> _trainUnitInitialized;
        public IObservable<TrainUnitCreatedData> TrainUnitInitializedEvent => _trainUnitInitialized.AsObservable();

        public TrainUnitInitializationNotifier()
        {
            _trainUnitInitialized = new Subject<TrainUnitCreatedData>();
        }

        public void Notify(TrainUnit trainUnit)
        {
            // 列車生成通知を発行する
            // Publish train unit creation notification
            _trainUnitInitialized?.OnNext(new TrainUnitCreatedData(trainUnit));
        }

        public void Dispose()
        {
            // 通知用Subjectを破棄する
            // Dispose the notification subject
            _trainUnitInitialized?.Dispose();
        }

        public readonly struct TrainUnitCreatedData
        {
            public readonly TrainUnit TrainUnit;

            public TrainUnitCreatedData(TrainUnit trainUnit)
            {
                TrainUnit = trainUnit;
            }
        }
    }
}
