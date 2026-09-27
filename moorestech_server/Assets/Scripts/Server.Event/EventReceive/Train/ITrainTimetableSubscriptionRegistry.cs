using System.Collections.Generic;
using Game.Train.Unit;

namespace Server.Event.EventReceive.Train
{
    // 時刻表イベントの配信先プレイヤーを決める購読簿
    // Subscription registry deciding which players receive a train's timetable event
    public interface ITrainTimetableSubscriptionRegistry
    {
        IReadOnlyList<int> GetSubscribers(TrainUnit trainUnit);
    }
}
