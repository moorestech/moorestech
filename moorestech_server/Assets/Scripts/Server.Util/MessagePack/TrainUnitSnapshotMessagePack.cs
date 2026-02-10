using Game.Train.Diagram;
using Game.Train.RailPositions;
using Game.Train.Unit;
using MessagePack;
using System;
using System.Collections.Generic;
using System.Linq;
using Game.Train.SaveLoad;

namespace Server.Util.MessagePack
{
    public enum TrainDiagramDepartureConditionType
    {
        TrainInventoryFull = 0,
        TrainInventoryEmpty = 1,
        WaitForTicks = 2
    }

    [MessagePackObject]
    public class TrainUnitSnapshotBundleMessagePack
    {
        [Key(0)] public TrainSimulationSnapshotMessagePack Simulation { get; set; }
        [Key(1)] public TrainDiagramSnapshotMessagePack Diagram { get; set; }
        [Key(2)] public RailPositionSnapshotMessagePack RailPosition { get; set; }

        [Obsolete("Reserved for MessagePack serialization.")]
        public TrainUnitSnapshotBundleMessagePack() { }

        public TrainUnitSnapshotBundleMessagePack(TrainUnitSnapshotBundle bundle)
        {
            Simulation = new TrainSimulationSnapshotMessagePack(bundle.Simulation);
            Diagram = new TrainDiagramSnapshotMessagePack(bundle.Diagram);
            RailPosition = new RailPositionSnapshotMessagePack(bundle.RailPositionSnapshot);
        }

        public TrainUnitSnapshotBundle ToModel()
        {
            return new TrainUnitSnapshotBundle(
                Simulation.ToModel(),
                Diagram.ToModel(),
                RailPosition.ToModel());
        }
    }

    [MessagePackObject]
    public class TrainSimulationSnapshotMessagePack
    {
        [Key(0)] public Guid TrainId { get; set; }
        [Key(1)] public double CurrentSpeed { get; set; }
        [Key(2)] public double AccumulatedDistance { get; set; }
        [Key(3)] public int MasconLevel { get; set; }
        [Key(4)] public bool IsAutoRun { get; set; }
        [Key(5)] public bool IsDocked { get; set; }
        [Key(6)] public List<TrainCarSnapshotMessagePack> Cars { get; set; }

        [Obsolete("Reserved for MessagePack serialization.")]
        public TrainSimulationSnapshotMessagePack() { }

        public TrainSimulationSnapshotMessagePack(TrainSimulationSnapshot snapshot)
        {
            TrainId = snapshot.TrainId;
            CurrentSpeed = snapshot.CurrentSpeed;
            AccumulatedDistance = snapshot.AccumulatedDistance;
            MasconLevel = snapshot.MasconLevel;
            IsAutoRun = snapshot.IsAutoRun;
            IsDocked = snapshot.IsDocked;
            Cars = snapshot.Cars?.Select(car => new TrainCarSnapshotMessagePack(car)).ToList()
                   ?? new List<TrainCarSnapshotMessagePack>();
        }

        public TrainSimulationSnapshot ToModel()
        {
            var cars = Cars?.Select(car => car.ToModel()).ToArray() ?? Array.Empty<TrainCarSnapshot>();
            return new TrainSimulationSnapshot(
                TrainId,
                CurrentSpeed,
                AccumulatedDistance,
                MasconLevel,
                IsAutoRun,
                IsDocked,
                cars);
        }
    }

    [MessagePackObject]
    public class TrainCarSnapshotMessagePack
    {
        [Key(0)] public long TrainCarInstanceId { get; set; }
        [Key(1)] public int InventorySlotsCount { get; set; }
        [Key(2)] public int TractionForce { get; set; }
        [Key(3)] public bool IsFacingForward { get; set; }
        [Key(4)] public Guid TrainCarMasterId { get; set; }

        [Obsolete("Reserved for MessagePack serialization.")]
        public TrainCarSnapshotMessagePack() { }

        public TrainCarSnapshotMessagePack(TrainCarSnapshot snapshot)
        {
            TrainCarInstanceId = snapshot.TrainCarInstanceId.AsPrimitive();
            InventorySlotsCount = snapshot.InventorySlotsCount;
            TractionForce = snapshot.TractionForce;
            IsFacingForward = snapshot.IsFacingForward;
            TrainCarMasterId = snapshot.TrainCarMasterId;
        }

        public TrainCarSnapshot ToModel()
        {
            return new TrainCarSnapshot(new TrainCarInstanceId(TrainCarInstanceId), TrainCarMasterId, InventorySlotsCount, TractionForce, IsFacingForward);
        }
    }

    [MessagePackObject]
    public class TrainDiagramSnapshotMessagePack
    {
        [Key(0)] public int CurrentIndex { get; set; }
        [Key(1)] public List<TrainDiagramEntrySnapshotMessagePack> Entries { get; set; }

        [Obsolete("Reserved for MessagePack serialization.")]
        public TrainDiagramSnapshotMessagePack() { }

        public TrainDiagramSnapshotMessagePack(TrainDiagramSnapshot snapshot)
        {
            CurrentIndex = snapshot.CurrentIndex;
            Entries = snapshot.Entries?.Select(entry => new TrainDiagramEntrySnapshotMessagePack(entry)).ToList()
                      ?? new List<TrainDiagramEntrySnapshotMessagePack>();
        }

        public TrainDiagramSnapshot ToModel()
        {
            var entrySnapshots = Entries?.Select(entry => entry.ToModel()).ToArray()
                                ?? Array.Empty<TrainDiagramEntrySnapshot>();
            return new TrainDiagramSnapshot(CurrentIndex, entrySnapshots);
        }
    }

    [MessagePackObject]
    public class TrainDiagramEntrySnapshotMessagePack
    {
        [Key(0)] public Guid EntryId { get; set; }
        [Key(1)] public ConnectionDestinationMessagePack Node { get; set; }
        [Key(2)] public TrainDiagramDepartureConditionType[] DepartureConditions { get; set; }
        [Key(3)] public int? WaitForTicksInitial { get; set; }
        [Key(4)] public int? WaitForTicksRemaining { get; set; }

        [Obsolete("Reserved for MessagePack serialization.")]
        public TrainDiagramEntrySnapshotMessagePack() { }

        public TrainDiagramEntrySnapshotMessagePack(TrainDiagramEntrySnapshot snapshot)
        {
            EntryId = snapshot.EntryId;
            Node = new ConnectionDestinationMessagePack(snapshot.Node);
            DepartureConditions = snapshot.DepartureConditions?
                .Select(ConvertToMessagePackCondition)
                .ToArray() ?? Array.Empty<TrainDiagramDepartureConditionType>();
            WaitForTicksInitial = snapshot.WaitForTicksInitial;
            WaitForTicksRemaining = snapshot.WaitForTicksRemaining;
        }

        public TrainDiagramEntrySnapshot ToModel()
        {
            var destination = Node?.ToModel() ?? default;
            var conditions = DepartureConditions?
                .Select(ConvertToModelCondition)
                .ToArray() ?? Array.Empty<TrainDiagram.DepartureConditionType>();
            return new TrainDiagramEntrySnapshot(EntryId, destination, conditions, WaitForTicksInitial, WaitForTicksRemaining);
        }

        private static TrainDiagram.DepartureConditionType ConvertToModelCondition(TrainDiagramDepartureConditionType condition)
        {
            return (TrainDiagram.DepartureConditionType)(int)condition;
        }

        private static TrainDiagramDepartureConditionType ConvertToMessagePackCondition(TrainDiagram.DepartureConditionType condition)
        {
            return (TrainDiagramDepartureConditionType)(int)condition;
        }
    }

    [MessagePackObject]
    public class RailPositionSnapshotMessagePack
    {
        [Key(0)] public int TrainLength { get; set; }
        [Key(1)] public int DistanceToNextNode { get; set; }
        [Key(2)] public List<ConnectionDestinationMessagePack> RailNodes { get; set; }

        [Obsolete("Reserved for MessagePack serialization.")]
        public RailPositionSnapshotMessagePack() { }

        public RailPositionSnapshotMessagePack(RailPositionSaveData saveData)
        {
            TrainLength = saveData.TrainLength;
            DistanceToNextNode = saveData.DistanceToNextNode;
            RailNodes = saveData.RailSnapshot?.Select(node => new ConnectionDestinationMessagePack(node)).ToList()
                        ?? new List<ConnectionDestinationMessagePack>();
        }

        public RailPositionSaveData ToModel()
        {
            return new RailPositionSaveData
            {
                TrainLength = TrainLength,
                DistanceToNextNode = DistanceToNextNode,
                RailSnapshot = RailNodes?.Select(node => node.ToModel()).ToList()
                               ?? new List<ConnectionDestination>()
            };
        }
    }
}
