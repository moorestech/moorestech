using System;
using System.Collections.Generic;
using Game.Train.SaveLoad;
using Game.Train.Unit;

namespace Game.Train.Diagram
{
    // ダイアグラム状態のハッシュを計算し差分確認に使う
    // Provides a deterministic hash for validating diagram consistency
    public static class TrainDiagramHashCalculator
    {
        private const uint FnvOffset = 2166136261;
        private const uint FnvPrime = 16777619;
        private const int EntryIndexMixSalt = unchecked((int)0x5F3759D5);
        private const int NullEntryMixSalt = unchecked((int)0x11C3A55B);

        // サーバー側のTrainDiagramから即時計算する
        // Computes a hash directly from the live TrainDiagram
        public static uint Compute(TrainDiagram diagram)
        {
            if (diagram == null)
            {
                return 0;
            }

            return ComputeFromEntries(diagram.CurrentIndex, diagram.Entries);
        }

        // スナップショット構造体から同一アルゴリズムで算出
        // Computes the same hash from a TrainDiagramSnapshot
        public static uint Compute(TrainDiagramSnapshot snapshot)
        {
            return ComputeFromSnapshots(snapshot.CurrentIndex, snapshot.Entries);
        }

        #region Internal

        // Liveエントリ列を巡回してハッシュ化
        // Mix every live entry inside the diagram
        private static uint ComputeFromEntries(int currentIndex, IReadOnlyList<TrainDiagramEntry> entries)
        {
            uint hash = Mix(FnvOffset, currentIndex);
            if (entries == null)
            {
                return hash;
            }

            for (var i = 0; i < entries.Count; i++)
            {
                var entryState = ConvertEntryToHashInput(entries[i]);
                hash = MixEntry(hash, i, entryState);
            }

            return hash;

            #region Internal

            DiagramHashEntryState ConvertEntryToHashInput(TrainDiagramEntry entry)
            {
                if (entry == null)
                {
                    return DiagramHashEntryState.Empty;
                }

                return new DiagramHashEntryState(
                    true,
                    entry.entryId,
                    entry.Node?.ConnectionDestination ?? ConnectionDestination.Default,
                    entry.DepartureConditionTypes,
                    entry.GetWaitForTicksInitialTicks(),
                    entry.GetWaitForTicksRemainingTicks());
            }

            #endregion
        }

        // Snapshot列を巡回してハッシュ化
        // Mix every entry snapshot to reconstruct the same hash
        private static uint ComputeFromSnapshots(int currentIndex, IReadOnlyList<TrainDiagramEntrySnapshot> entries)
        {
            uint hash = Mix(FnvOffset, currentIndex);
            if (entries == null)
            {
                return hash;
            }

            for (var i = 0; i < entries.Count; i++)
            {
                var entryState = ConvertEntryToHashInput(entries[i]);
                hash = MixEntry(hash, i, entryState);
            }

            return hash;

            #region Internal

            DiagramHashEntryState ConvertEntryToHashInput(TrainDiagramEntrySnapshot entry)
            {
                return new DiagramHashEntryState(
                    true,
                    entry.EntryId,
                    entry.Node,
                    entry.DepartureConditions,
                    entry.WaitForTicksInitial,
                    entry.WaitForTicksRemaining);
            }

            #endregion
        }

        // Live/Snapshotを共通入力へ正規化して1回で混合する
        // Convert both live/snapshot into one deterministic input and mix once
        private static uint MixEntry(uint current, int entryIndex, DiagramHashEntryState entryState)
        {
            var hash = Mix(current, EntryIndexMixSalt ^ entryIndex);
            if (!entryState.HasEntry)
            {
                return Mix(hash, NullEntryMixSalt);
            }

            hash = MixGuid(hash, entryState.EntryId);
            hash = MixDestination(hash, entryState.Destination);
            hash = MixConditions(hash, entryState.DepartureConditions);
            hash = MixOptional(hash, entryState.WaitForTicksInitial);
            hash = MixOptional(hash, entryState.WaitForTicksRemaining);
            return hash;
        }

        private static uint MixConditions(uint current, IReadOnlyList<TrainDiagram.DepartureConditionType> conditions)
        {
            if (conditions == null || conditions.Count == 0)
            {
                return Mix(current, -1);
            }

            var hash = Mix(current, conditions.Count);
            for (var i = 0; i < conditions.Count; i++)
            {
                hash = Mix(hash, (int)conditions[i]);
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

        private static uint MixOptional(uint current, int? value)
        {
            if (!value.HasValue)
            {
                return Mix(current, 0x7FFFFFFF);
            }
            return Mix(current, value.Value);
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

        private readonly struct DiagramHashEntryState
        {
            public static DiagramHashEntryState Empty => new DiagramHashEntryState(false, Guid.Empty, ConnectionDestination.Default, null, null, null);

            public DiagramHashEntryState(
                bool hasEntry,
                Guid entryId,
                ConnectionDestination destination,
                IReadOnlyList<TrainDiagram.DepartureConditionType> departureConditions,
                int? waitForTicksInitial,
                int? waitForTicksRemaining)
            {
                HasEntry = hasEntry;
                EntryId = entryId;
                Destination = destination;
                DepartureConditions = departureConditions;
                WaitForTicksInitial = waitForTicksInitial;
                WaitForTicksRemaining = waitForTicksRemaining;
            }

            public bool HasEntry { get; }
            public Guid EntryId { get; }
            public ConnectionDestination Destination { get; }
            public IReadOnlyList<TrainDiagram.DepartureConditionType> DepartureConditions { get; }
            public int? WaitForTicksInitial { get; }
            public int? WaitForTicksRemaining { get; }
        }

        #endregion
    }
}
