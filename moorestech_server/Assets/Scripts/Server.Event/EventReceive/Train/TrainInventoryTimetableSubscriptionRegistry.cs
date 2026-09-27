using System.Collections.Generic;
using Game.PlayerInventory.Interface.Subscription;
using Game.Train.Unit;

namespace Server.Event.EventReceive.Train
{
    // 時刻表タブは列車インベントリ画面の中にあるため、インベントリ購読簿をそのまま配信先とする
    // The timetable tab lives inside the train inventory screen, so the inventory subscription store decides the recipients
    public sealed class TrainInventoryTimetableSubscriptionRegistry : ITrainTimetableSubscriptionRegistry
    {
        private readonly IInventorySubscriptionStore _inventorySubscriptionStore;

        public TrainInventoryTimetableSubscriptionRegistry(IInventorySubscriptionStore inventorySubscriptionStore)
        {
            _inventorySubscriptionStore = inventorySubscriptionStore;
        }

        public IReadOnlyList<int> GetSubscribers(TrainUnit trainUnit)
        {
            // 編成のどの車両を開いていても同じ列車の時刻表なので、車両ごとの購読者を重複なく集める
            // Any car of the formation shows the same timetable, so gather each car's subscribers without duplicates
            var playerIds = new List<int>();
            foreach (var car in trainUnit.Cars)
            {
                var identifier = new TrainInventorySubInventoryIdentifier(car.TrainCarInstanceId.AsPrimitive());
                foreach (var playerId in _inventorySubscriptionStore.GetSubscribers(identifier))
                {
                    if (playerIds.Contains(playerId)) continue;
                    playerIds.Add(playerId);
                }
            }
            return playerIds;
        }
    }
}
