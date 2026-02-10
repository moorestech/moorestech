using System;
using System.Collections.Generic;
using Game.Train.Diagram;
using Game.Train.RailPositions;
using Game.Train.SaveLoad;

namespace Game.Train.Unit
{
    // TrainUnitスナップショットのハッシュを計算する
    // Computes a deterministic hash for TrainUnit snapshots
    public static class TrainUnitSnapshotHashCalculator
    {
        private const uint FnvOffset = 2166136261;
        private const uint FnvPrime = 16777619;

        // TrainUnit全体の状態ハッシュを算出する
        // Compute the hash for the entire train unit snapshot set
        public static uint Compute(IReadOnlyList<TrainUnitSnapshotBundle> bundles)
        {
            var count = bundles?.Count ?? 0;
            uint hash = Mix(FnvOffset, count);
            if (count == 0)
            {
                return hash;
            }

            // TrainId順に並べて順序非依存にする
            // Sort by TrainId to keep the hash order-independent
            var ordered = new List<TrainUnitSnapshotBundle>(bundles);
            ordered.Sort((left, right) => left.Simulation.TrainId.CompareTo(right.Simulation.TrainId));

            for (var i = 0; i < ordered.Count; i++)
            {
                hash = Mix(hash, i);
                hash = MixBundle(hash, ordered[i]);
            }

            return hash;
        }

        #region Internal

        private static uint MixBundle(uint current, TrainUnitSnapshotBundle bundle)
        {
            // Simulation/Diagram/RailPositionの要素を順に混ぜる
            // Mix simulation, diagram, and rail position fields in order
            var simulation = bundle.Simulation;
            var hash = MixGuid(current, simulation.TrainId);
            hash = MixLong(hash, BitConverter.DoubleToInt64Bits(simulation.CurrentSpeed));
            hash = MixLong(hash, BitConverter.DoubleToInt64Bits(simulation.AccumulatedDistance));
            hash = Mix(hash, simulation.MasconLevel);
            hash = Mix(hash, simulation.IsAutoRun ? 1 : 0);
            hash = Mix(hash, simulation.IsDocked ? 1 : 0);
            hash = MixCars(hash, simulation.Cars);

            var diagramHash = TrainDiagramHashCalculator.Compute(bundle.Diagram);
            hash = Mix(hash, unchecked((int)diagramHash));
            hash = MixRailPosition(hash, bundle.RailPositionSnapshot);
            return hash;
        }

        private static uint MixCars(uint current, IReadOnlyList<TrainCarSnapshot> cars)
        {
            if (cars == null || cars.Count == 0)
            {
                return Mix(current, -1);
            }

            var hash = Mix(current, cars.Count);
            for (var i = 0; i < cars.Count; i++)
            {
                var car = cars[i];
                hash = MixLong(hash, car.TrainCarInstanceId.AsPrimitive());
                hash = MixGuid(hash, car.TrainCarMasterId);
                hash = Mix(hash, car.InventorySlotsCount);
                hash = Mix(hash, car.TractionForce);
                hash = Mix(hash, car.IsFacingForward ? 1 : 0);
            }

            return hash;
        }

        private static uint MixRailPosition(uint current, RailPositionSaveData saveData)
        {
            if (saveData == null)
            {
                return Mix(current, -1);
            }

            var hash = Mix(current, saveData.TrainLength);
            hash = Mix(hash, saveData.DistanceToNextNode);

            var nodes = saveData.RailSnapshot;
            if (nodes == null || nodes.Count == 0)
            {
                return Mix(hash, -1);
            }

            hash = Mix(hash, nodes.Count);
            for (var i = 0; i < nodes.Count; i++)
            {
                hash = MixDestination(hash, nodes[i]);
            }

            return hash;
        }

        private static uint MixDestination(uint current, ConnectionDestination destination)
        {
            if (destination.IsDefault())
            {
                return Mix(current, -1);
            }

            var position = destination.blockPosition;
            var hash = Mix(current, position.x);
            hash = Mix(hash, position.y);
            hash = Mix(hash, position.z);
            hash = Mix(hash, destination.componentIndex);
            hash = Mix(hash, destination.IsFront ? 1 : 0);
            return hash;
        }

        private static uint MixGuid(uint current, Guid guid)
        {
            if (guid == Guid.Empty)
            {
                return Mix(current, 0);
            }

            var bytes = guid.ToByteArray();
            for (var i = 0; i < bytes.Length; i += 4)
            {
                var chunk = BitConverter.ToInt32(bytes, i);
                current = Mix(current, chunk);
            }
            return current;
        }

        private static uint MixLong(uint current, long value)
        {
            unchecked
            {
                current = Mix(current, (int)value);
                current = Mix(current, (int)(value >> 32));
                return current;
            }
        }

        private static uint Mix(uint current, int value)
        {
            unchecked
            {
                var result = current ^ (uint)value;
                result *= FnvPrime;
                return result;
            }
        }

        #endregion
    }
}
