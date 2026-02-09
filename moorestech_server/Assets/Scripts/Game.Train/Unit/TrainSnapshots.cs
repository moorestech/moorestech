using Core.Item.Interface;
using Game.Train.Diagram;
using Game.Train.RailPositions;
using System;
using System.Collections.Generic;
using Game.Train.SaveLoad;

namespace Game.Train.Unit
{
    [Serializable]
    public class TrainUnitSaveData
    {
        public RailPositionSaveData railPositionSaveData { get; set; }
        public bool IsAutoRun { get; set; }
        public Guid PreviousEntryGuid { get; set; }
        public long? CurrentSpeedBits { get; set; }
        public long? AccumulatedDistanceBits { get; set; }
        public List<TrainCarSaveData> Cars { get; set; }
        public TrainDiagramSaveData Diagram { get; set; }
    }

    [Serializable]
    public class TrainCarSaveData
    {
        public Guid TrainCarGuid { get; set; }
        public bool IsFacingForward { get; set; }
        public SerializableVector3Int? DockingBlockPosition { get; set; }
        public List<ItemStackSaveJsonObject> InventoryItems { get; set; }
        public List<ItemStackSaveJsonObject> FuelItems { get; set; }
    }

    [Serializable]
    public class TrainDiagramSaveData
    {
        public int CurrentIndex { get; set; }
        public List<TrainDiagramEntrySaveData> Entries { get; set; }
    }

    [Serializable]
    public class TrainDiagramEntrySaveData
    {
        public Guid EntryId { get; set; }
        public ConnectionDestination Node { get; set; }
        public List<TrainDiagram.DepartureConditionType> DepartureConditions { get; set; }
        public int? WaitForTicksInitial { get; set; }
        public int? WaitForTicksRemaining { get; set; }
    }
    // -----------------saveData関連ここまで-----------------




    // -----------------クライアント関連-----------------
    public readonly struct TrainCarSnapshot
    {
        public TrainCarSnapshot(Guid trainCarInstanceGuid, Guid trainCarMasterId, int inventorySlotsCount, int tractionForce, bool isFacingForward)
        {
            TrainCarInstanceGuid = trainCarInstanceGuid;
            TrainCarMasterId = trainCarMasterId;
            IsFacingForward = isFacingForward;
            InventorySlotsCount = inventorySlotsCount;
            TractionForce = tractionForce;
        }

        public Guid TrainCarInstanceGuid { get; }
        public Guid TrainCarMasterId { get; }
        public bool IsFacingForward { get; }
        public int InventorySlotsCount { get; }
        public int TractionForce { get; }
    }

    // 列車のシミュレーション状態をクライアントにおくる構造体
    // Per-tick snapshot of the simulation specific state
    public readonly struct TrainSimulationSnapshot
    {
        public TrainSimulationSnapshot(
            Guid trainId,
            double currentSpeed,
            double accumulatedDistance,
            int masconLevel,
            bool isAutoRun,
            bool isDocked,
            IReadOnlyList<TrainCarSnapshot> cars)
        {
            TrainId = trainId;
            CurrentSpeed = currentSpeed;
            AccumulatedDistance = accumulatedDistance;
            MasconLevel = masconLevel;
            IsAutoRun = isAutoRun;
            IsDocked = isDocked;
            Cars = cars;
        }

        public Guid TrainId { get; }
        public double CurrentSpeed { get; }
        public double AccumulatedDistance { get; }
        public int MasconLevel { get; }
        public bool IsAutoRun { get; }
        public bool IsDocked { get; }
        public IReadOnlyList<TrainCarSnapshot> Cars { get; }
    }

    // ダイアグラムの各エントリを転送しやすい形へ整形
    // Diagram entry snapshot that can be transferred easily
    public readonly struct TrainDiagramEntrySnapshot
    {
        public TrainDiagramEntrySnapshot(
            Guid entryId,
            ConnectionDestination node,
            IReadOnlyList<TrainDiagram.DepartureConditionType> departureConditions,
            int? waitForTicksInitial,
            int? waitForTicksRemaining)
        {
            EntryId = entryId;
            Node = node;
            DepartureConditions = departureConditions;
            WaitForTicksInitial = waitForTicksInitial;
            WaitForTicksRemaining = waitForTicksRemaining;
        }

        public Guid EntryId { get; }
        public ConnectionDestination Node { get; }
        public IReadOnlyList<TrainDiagram.DepartureConditionType> DepartureConditions { get; }
        public int? WaitForTicksInitial { get; }
        public int? WaitForTicksRemaining { get; }
    }

    // 列車ダイアグラム全体のスナップショット
    // Snapshot representing the entire train diagram
    public readonly struct TrainDiagramSnapshot
    {
        public TrainDiagramSnapshot(int currentIndex, IReadOnlyList<TrainDiagramEntrySnapshot> entries)
        {
            CurrentIndex = currentIndex;
            Entries = entries;
        }

        public int CurrentIndex { get; }
        public IReadOnlyList<TrainDiagramEntrySnapshot> Entries { get; }
    }

    // シミュレーションとダイアグラムをまとめて扱うためのバンドル
    // Bundle grouping both simulation and diagram snapshots
    public readonly struct TrainUnitSnapshotBundle
    {
        public TrainUnitSnapshotBundle(TrainSimulationSnapshot simulation, TrainDiagramSnapshot diagram, RailPositionSaveData railPositionSnapshot)
        {
            RailPositionSnapshot = railPositionSnapshot;
            Simulation = simulation;
            Diagram = diagram;
        }

        public RailPositionSaveData RailPositionSnapshot { get; }
        public TrainSimulationSnapshot Simulation { get; }
        public TrainDiagramSnapshot Diagram { get; }
    }
    // -----------------クライアント関連ここまで-----------------
}
