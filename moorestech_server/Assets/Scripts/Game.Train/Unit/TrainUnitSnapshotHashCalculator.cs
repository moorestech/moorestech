using System.Collections.Generic;
using ClassLibrary;
using Game.Train.RailPositions;
using Game.Train.SaveLoad;

namespace Game.Train.Unit
{
    // TrainUnit スナップショットのハッシュ。列車ごとに独立したハッシュ(識別子は TrainUnitInstanceId)を作り、順序に依存しない加算で畳む(ElementHash。ベルト・流体と同じ部品)
    // Hash of the TrainUnit snapshots: each train gets an independent hash (identified by TrainUnitInstanceId) folded by order-independent addition (ElementHash, shared with belt and fluid)
    // 識別子が列車自身にあるので並べ替えは要らず、どの順で渡しても同じ値になる
    // The identity lives in the train itself, so no sorting is needed and any input order gives the same value
    public static class TrainUnitSnapshotHashCalculator
    {
        private const uint UnitSeed = 0x756E6974;

        public static uint Compute(IReadOnlyList<TrainUnitSnapshotBundle> bundles)
        {
            unchecked
            {
                var count = bundles?.Count ?? 0;
                var sum = ElementHash.Count(UnitSeed, count);
                for (var i = 0; i < count; i++) sum += ElementHash.Finish(AddBundle(UnitSeed, bundles[i]));
                return sum;
            }

            #region Internal

            static uint AddBundle(uint current, TrainUnitSnapshotBundle bundle)
            {
                // 識別子→走行状態→車両→線路上の位置の順に取り込む
                // Take in the identity, then the motion state, the cars and the rail position
                var simulation = bundle.Simulation;
                var hash = ElementHash.Add(current, simulation.TrainUnitInstanceId.AsPrimitive());
                hash = ElementHash.Add(hash, simulation.CurrentSpeed);
                hash = ElementHash.Add(hash, simulation.AccumulatedDistance);
                hash = ElementHash.Add(hash, simulation.MasconLevel);
                hash = ElementHash.Add(hash, simulation.ManualBranchSelectionIndex);
                hash = AddCars(hash, simulation.Cars);
                return AddRailPosition(hash, bundle.RailPositionSnapshot);
            }

            static uint AddCars(uint current, IReadOnlyList<TrainCarSnapshot> cars)
            {
                if (cars == null || cars.Count == 0) return ElementHash.Add(current, -1);

                var hash = ElementHash.Add(current, cars.Count);
                for (var i = 0; i < cars.Count; i++)
                {
                    var car = cars[i];
                    hash = ElementHash.Add(hash, car.TrainCarInstanceId.AsPrimitive());
                    hash = ElementHash.Add(hash, car.TrainCarMasterId);
                    hash = ElementHash.Add(hash, car.IsFacingForward);
                    hash = ElementHash.Add(hash, car.Weight);
                }
                return hash;
            }

            static uint AddRailPosition(uint current, RailPositionSaveData saveData)
            {
                if (saveData == null) return ElementHash.Add(current, -1);

                var hash = ElementHash.Add(current, saveData.TrainLength);
                hash = ElementHash.Add(hash, saveData.DistanceToNextNode);

                var nodes = saveData.RailSnapshot;
                if (nodes == null || nodes.Count == 0) return ElementHash.Add(hash, -1);

                hash = ElementHash.Add(hash, nodes.Count);
                for (var i = 0; i < nodes.Count; i++) hash = AddDestination(hash, nodes[i]);
                return hash;
            }

            static uint AddDestination(uint current, ConnectionDestination destination)
            {
                if (destination.IsDefault()) return ElementHash.Add(current, -1);

                var position = destination.blockPosition;
                var hash = ElementHash.Add(current, position.x);
                hash = ElementHash.Add(hash, position.y);
                hash = ElementHash.Add(hash, position.z);
                hash = ElementHash.Add(hash, destination.componentIndex);
                return ElementHash.Add(hash, destination.IsFront);
            }

            #endregion
        }
    }
}
