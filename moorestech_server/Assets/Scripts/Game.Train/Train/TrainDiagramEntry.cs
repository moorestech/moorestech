using Game.Train.RailGraph;
using System;
using System.Collections.Generic;

namespace Game.Train.Train
{
    public interface ITrainDiagramDepartureCondition
    {
        bool CanDepart(TrainUnit trainUnit);
    }

    internal abstract class TrainDiagramInventoryConditionBase : ITrainDiagramDepartureCondition
    {
        public bool CanDepart(TrainUnit trainUnit)
        {
            if (trainUnit.Cars == null)
                return true;

            foreach (var car in trainUnit.Cars)
            {
                if (!car.IsDocked)
                {
                    continue;
                }

                if (!MatchesInventoryState(car))
                {
                    return false;
                }
            }
            return true;
        }

        protected abstract bool MatchesInventoryState(TrainCar car);
    }

    internal sealed class TrainDiagramInventoryFullCondition : TrainDiagramInventoryConditionBase
    {
        protected override bool MatchesInventoryState(TrainCar car) => car.IsInventoryFull();
    }

    internal sealed class TrainDiagramInventoryEmptyCondition : TrainDiagramInventoryConditionBase
    {
        protected override bool MatchesInventoryState(TrainCar car) => car.IsInventoryEmpty();
    }

    internal sealed class TrainDiagramWaitForTicksCondition : ITrainDiagramDepartureCondition
    {
        private int _initialTicks;
        private int _remainingTicks;

        public int InitialTicks => _initialTicks;
        public int RemainingTicks => _remainingTicks;

        public void Configure(int ticks)
        {
            if (ticks < 0)
            {
                ticks = 0;
            }
            _initialTicks = ticks;
            _remainingTicks = ticks;
        }

        public void Reset()
        {
            _remainingTicks = _initialTicks;
        }

        public void Restore(int initialTicks, int remainingTicks)
        {
            _initialTicks = Math.Max(initialTicks, 0);
            _remainingTicks = Math.Max(Math.Min(remainingTicks, _initialTicks), 0);
        }

        public bool CanDepart(TrainUnit trainUnit)
        {
            if (_remainingTicks <= 0)
            {
                return true;
            }

            if (trainUnit == null || !trainUnit.IsAutoRun)
            {
                return false;
            }

            var docking = trainUnit.trainUnitStationDocking;
            if (docking == null || !docking.IsDocked)
            {
                return false;
            }

            _remainingTicks--;
            return _remainingTicks <= 0;
        }
    }

    public sealed class TrainDiagramEntry
    {
        private readonly List<ITrainDiagramDepartureCondition> _departureConditions;
        private readonly List<TrainDiagram.DepartureConditionType> _departureConditionTypes;
        private TrainDiagramWaitForTicksCondition _waitForTicksCondition;

        public TrainDiagramEntry(IRailNode node)
        {
            Node = node;
            entryId = Guid.NewGuid();
            _departureConditions = new List<ITrainDiagramDepartureCondition>();
            _departureConditionTypes = new List<TrainDiagram.DepartureConditionType>();
        }

        public IRailNode Node { get; private set; }
        public Guid entryId { get; private set; }

        public IReadOnlyList<ITrainDiagramDepartureCondition> DepartureConditions => _departureConditions;
        public IReadOnlyList<TrainDiagram.DepartureConditionType> DepartureConditionTypes => _departureConditionTypes;

        public int? GetWaitForTicksInitialTicks() => _waitForTicksCondition?.InitialTicks;
        public int? GetWaitForTicksRemainingTicks() => _waitForTicksCondition?.RemainingTicks;

        public bool MatchesNode(IRailNode node)
        {
            if (Node == null || node == null)
            {
                return ReferenceEquals(Node, node);
            }
            return ReferenceEquals(Node, node) || Node.NodeId == node.NodeId;
        }

        public bool CanDepart(TrainUnit trainUnit)
        {
            if (_departureConditions.Count == 0)
            {
                return true;
            }

            foreach (var condition in _departureConditions)
            {
                if (!condition.CanDepart(trainUnit))
                {
                    return false;
                }
            }

            return true;
        }

        public void SetDepartureCondition(TrainDiagram.DepartureConditionType conditionType)
        {
            SetDepartureConditions(new[] { conditionType });
        }

        public void SetDepartureConditions(IEnumerable<TrainDiagram.DepartureConditionType> conditionTypes)
        {
            _departureConditions.Clear();
            _departureConditionTypes.Clear();
            _waitForTicksCondition = null;

            if (conditionTypes == null)
            {
                return;
            }

            foreach (var conditionType in conditionTypes)
            {
                AddDepartureCondition(conditionType);
            }
        }

        public void AddDepartureCondition(TrainDiagram.DepartureConditionType conditionType)
        {
            var condition = CreateDepartureCondition(conditionType);
            if (condition == null)
            {
                return;
            }

            _departureConditions.Add(condition);
            _departureConditionTypes.Add(conditionType);
        }

        public bool RemoveDepartureCondition(TrainDiagram.DepartureConditionType conditionType)
        {
            for (var i = 0; i < _departureConditionTypes.Count; i++)
            {
                if (_departureConditionTypes[i] != conditionType)
                {
                    continue;
                }

                _departureConditionTypes.RemoveAt(i);
                _departureConditions.RemoveAt(i);
                if (conditionType == TrainDiagram.DepartureConditionType.WaitForTicks)
                {
                    _waitForTicksCondition = null;
                }
                return true;
            }
            return false;
        }

        public void SetDepartureWaitTicks(int ticks)
        {
            SetDepartureConditions(new[] { TrainDiagram.DepartureConditionType.WaitForTicks });
            _waitForTicksCondition?.Configure(ticks);
        }

        public void OnDeparted()
        {
            _waitForTicksCondition?.Reset();
        }

        internal static TrainDiagramEntry CreateFromSaveData(
            IRailNode node,
            Guid entryGuid,
            IEnumerable<TrainDiagram.DepartureConditionType> conditionTypes,
            int? waitForTicksInitial,
            int? waitForTicksRemaining)
        {
            var entry = new TrainDiagramEntry(node)
            {
                entryId = entryGuid
            };

            entry.SetDepartureConditions(conditionTypes);
            if (waitForTicksInitial.HasValue && entry._waitForTicksCondition != null)
            {
                var remaining = waitForTicksRemaining ?? waitForTicksInitial.Value;
                entry._waitForTicksCondition.Restore(waitForTicksInitial.Value, remaining);
            }
            return entry;
        }

        private ITrainDiagramDepartureCondition CreateDepartureCondition(TrainDiagram.DepartureConditionType conditionType)
        {
            switch (conditionType)
            {
                case TrainDiagram.DepartureConditionType.TrainInventoryEmpty:
                    return new TrainDiagramInventoryEmptyCondition();
                case TrainDiagram.DepartureConditionType.WaitForTicks:
                    var waitCondition = new TrainDiagramWaitForTicksCondition();
                    _waitForTicksCondition = waitCondition;
                    return waitCondition;
                case TrainDiagram.DepartureConditionType.TrainInventoryFull:
                    return new TrainDiagramInventoryFullCondition();
                default:
                    return null;
            }
        }
    }
}
