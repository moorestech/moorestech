using System;
using System.Collections.Generic;
using Game.Train.Unit;

namespace Client.Game.InGame.Train.Unit
{
    internal static class ClientTrainCarSnapshots
    {
        public static bool TryRemove(
            IReadOnlyList<TrainCarSnapshot> cars,
            TrainCarInstanceId trainCarInstanceId,
            out IReadOnlyList<TrainCarSnapshot> remaining)
        {
            var localCars = cars ?? Array.Empty<TrainCarSnapshot>();
            remaining = localCars;
            if (localCars.Count == 0)
            {
                return false;
            }

            // 指定車両だけを除いた新しい一覧を作る
            // Build a new list without the specified car
            var nextCars = new List<TrainCarSnapshot>(localCars.Count);
            var removed = false;
            for (var i = 0; i < localCars.Count; i++)
            {
                var car = localCars[i];
                if (car.TrainCarInstanceId == trainCarInstanceId)
                {
                    removed = true;
                    continue;
                }
                nextCars.Add(car);
            }

            if (removed)
            {
                remaining = nextCars;
            }
            return removed;
        }

        public static IReadOnlyList<TrainCarSnapshot> Reverse(IReadOnlyList<TrainCarSnapshot> cars)
        {
            var localCars = cars ?? Array.Empty<TrainCarSnapshot>();
            if (localCars.Count == 0)
            {
                return localCars;
            }

            // 車両の順序と向きを同時に反転する
            // Reverse car order and facing together
            var reversed = new TrainCarSnapshot[localCars.Count];
            for (var i = 0; i < localCars.Count; i++)
            {
                var source = localCars[localCars.Count - 1 - i];
                reversed[i] = new TrainCarSnapshot(source.TrainCarInstanceId, source.TrainCarMasterId, !source.IsFacingForward, source.Weight);
            }
            return reversed;
        }
    }
}
