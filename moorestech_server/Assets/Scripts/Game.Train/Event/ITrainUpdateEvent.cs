using System;

namespace Game.Train.Event
{
    /// <summary>
    /// 列車関連イベント（インベントリ更新・削除）を扱うインターフェース
    /// Interface that exposes train inventory update and removal events.
    /// </summary>
    public interface ITrainUpdateEvent
    {
        IObservable<TrainInventoryUpdateEventProperties> OnInventoryUpdated { get; }
        IObservable<Guid> OnTrainRemoved { get; }
    }
}