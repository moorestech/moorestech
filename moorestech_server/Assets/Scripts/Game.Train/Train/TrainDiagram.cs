using System.Collections.Generic;
using System.Linq;
using Game.Train.RailGraph;

namespace Game.Train.Train
{
    public class TrainDiagram
    {
        private readonly TrainUnit _trainUnit;
        private readonly List<DiagramEntry> _entries;
        private int _currentIndex;

        public IReadOnlyList<DiagramEntry> Entries => _entries;
        public int CurrentIndex => _currentIndex;

        public enum DepartureConditionType
        {
            TrainInventoryFull,
            TrainInventoryEmpty,
            WaitForTicks
        }

        public interface IDepartureCondition
        {
            bool CanDepart(TrainUnit trainUnit);
        }

        public TrainDiagram(TrainUnit trainUnit)
        {
            _trainUnit = trainUnit;
            _entries = new List<DiagramEntry>();
            _currentIndex = -1;
        }

        public DiagramEntry AddEntry(RailNode node)
        {
            var entry = new DiagramEntry(node);
            _entries.Add(entry);
            return entry;
        }

        public bool CheckEntries()
        {
            if (!HasUsableEntry())
            {
                return false;
            }

            if (_currentIndex < 0)
            {
                return true;
            }

            if (_currentIndex >= _entries.Count)
            {
                _currentIndex = -1;
                return false;
            }

            if (!TryGetActiveEntry(out var currentEntry))
            {
                _currentIndex = -1;
                return false;
            }

            return currentEntry.CanDepart(_trainUnit);
        }

        public RailNode GetNextDestination()
        {
            return TryGetActiveEntry(out var entry) ? entry.Node : null;
        }

        public void MoveToNextEntry()
        {
            DiagramEntry previousEntry = null;
            if (TryGetActiveEntry(out var activeEntry))
            {
                previousEntry = activeEntry;
            }

            if (!HasUsableEntry())
            {
                _currentIndex = -1;
                previousEntry?.OnDeparted();
                return;
            }

            for (var i = 0; i < _entries.Count; i++)
            {
                _currentIndex = (_currentIndex + 1) % _entries.Count;
                if (_entries[_currentIndex].IsValid)
                {
                    previousEntry?.OnDeparted();
                    return;
                }
            }

            _currentIndex = -1;
            previousEntry?.OnDeparted();
        }

        public void HandleNodeRemoval(RailNode removedNode)
        {
            if (removedNode == null || _entries.Count == 0)
            {
                return;
            }

            var removedBeforeCurrent = 0;
            var removedAny = false;

            for (var i = _entries.Count - 1; i >= 0; i--)
            {
                if (!_entries[i].MatchesNode(removedNode))
                {
                    continue;
                }

                removedAny = true;

                if (_currentIndex >= 0 && i <= _currentIndex)
                {
                    removedBeforeCurrent++;
                }

                _entries.RemoveAt(i);
            }

            if (!removedAny)
            {
                return;
            }

            if (_entries.Count == 0)
            {
                _currentIndex = -1;
                return;
            }

            if (removedBeforeCurrent > 0)
            {
                _currentIndex -= removedBeforeCurrent;
            }

            if (_currentIndex >= _entries.Count)
            {
                _currentIndex = _entries.Count - 1;
            }

            if (_currentIndex < 0 || !_entries[_currentIndex].IsValid)
            {
                _currentIndex = -1;
            }
        }

        private bool HasUsableEntry()
        {
            if (_entries.Count == 0)
            {
                return false;
            }

            return _entries.Any(entry => entry.IsValid);
        }

        private bool TryGetActiveEntry(out DiagramEntry entry)
        {
            entry = null;

            if (_currentIndex < 0 || _currentIndex >= _entries.Count)
            {
                return false;
            }

            var candidate = _entries[_currentIndex];
            if (!candidate.IsValid)
            {
                return false;
            }

            entry = candidate;
            return true;
        }

        private abstract class TrainInventoryConditionBase : IDepartureCondition
        {
            public bool CanDepart(TrainUnit trainUnit)
            {
                if (trainUnit == null || trainUnit._cars == null || trainUnit._cars.Count == 0)
                {
                    return false;
                }

                var hasDockedCar = false;

                foreach (var car in trainUnit._cars)
                {
                    if (!car.IsDocked)
                    {
                        continue;
                    }

                    hasDockedCar = true;

                    if (!MatchesInventoryState(car))
                    {
                        return false;
                    }
                }

                return hasDockedCar;
            }

            protected abstract bool MatchesInventoryState(TrainCar car);
        }

        private sealed class TrainInventoryFullCondition : TrainInventoryConditionBase
        {
            protected override bool MatchesInventoryState(TrainCar car)
            {
                return car.IsInventoryFull();
            }
        }

        private sealed class TrainInventoryEmptyCondition : TrainInventoryConditionBase
        {
            protected override bool MatchesInventoryState(TrainCar car)
            {
                return car.IsInventoryEmpty();
            }
        }

        private sealed class WaitForTicksCondition : IDepartureCondition
        {
            private int _initialTicks;
            private int _remainingTicks;

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

                if (_remainingTicks > 0)
                {
                    _remainingTicks--;
                }

                return _remainingTicks <= 0;
            }
        }

        public sealed class DiagramEntry
        {
            public DiagramEntry(RailNode node)
            {
                Node = node;
                _departureConditions = new List<IDepartureCondition>();
                _departureConditionTypes = new List<DepartureConditionType>();
                SetDepartureCondition(DepartureConditionType.TrainInventoryFull);
            }

            public RailNode Node { get; private set; }

            public bool IsValid => Node != null;

            private readonly List<IDepartureCondition> _departureConditions;
            private readonly List<DepartureConditionType> _departureConditionTypes;
            private WaitForTicksCondition _waitForTicksCondition;

            public IReadOnlyList<IDepartureCondition> DepartureConditions => _departureConditions;
            public IReadOnlyList<DepartureConditionType> DepartureConditionTypes => _departureConditionTypes;

            public bool MatchesNode(RailNode node)
            {
                return Node == node;
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

            public void SetDepartureCondition(DepartureConditionType conditionType)
            {
                SetDepartureConditions(new[] { conditionType });
            }

            public void SetDepartureConditions(IEnumerable<DepartureConditionType> conditionTypes)
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

            public void AddDepartureCondition(DepartureConditionType conditionType)
            {
                var condition = CreateDepartureCondition(conditionType);
                if (condition == null)
                {
                    return;
                }

                _departureConditions.Add(condition);
                _departureConditionTypes.Add(conditionType);
            }

            public bool RemoveDepartureCondition(DepartureConditionType conditionType)
            {
                for (var i = 0; i < _departureConditionTypes.Count; i++)
                {
                    if (_departureConditionTypes[i] != conditionType)
                    {
                        continue;
                    }

                    _departureConditionTypes.RemoveAt(i);
                    _departureConditions.RemoveAt(i);
                    if (conditionType == DepartureConditionType.WaitForTicks)
                    {
                        _waitForTicksCondition = null;
                    }
                    return true;
                }

                return false;
            }

            public void SetDepartureWaitTicks(int ticks)
            {
                SetDepartureConditions(new[] { DepartureConditionType.WaitForTicks });
                _waitForTicksCondition?.Configure(ticks);
            }

            public void OnDeparted()
            {
                _waitForTicksCondition?.Reset();
            }

            private IDepartureCondition CreateDepartureCondition(DepartureConditionType conditionType)
            {
                switch (conditionType)
                {
                    case DepartureConditionType.TrainInventoryEmpty:
                        return new TrainInventoryEmptyCondition();
                    case DepartureConditionType.WaitForTicks:
                        var waitCondition = new WaitForTicksCondition();
                        _waitForTicksCondition = waitCondition;
                        return waitCondition;
                    case DepartureConditionType.TrainInventoryFull:
                        return new TrainInventoryFullCondition();
                    default:
                        return null;
                }
            }
        }
    }
}
