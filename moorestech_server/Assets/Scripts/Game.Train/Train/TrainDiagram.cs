using System.Collections.Generic;
using System.Linq;
using Game.Train.RailGraph;

namespace Game.Train.Train
{
    public class TrainDiagram
    {
        private readonly TrainUnit _trainUnit;
        public List<DiagramEntry> _entries;
        public int currentIndex;

        public TrainDiagram(TrainUnit trainUnit)
        {
            _trainUnit = trainUnit;
            _entries = new List<DiagramEntry>();
            currentIndex = -1;
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

            if (currentIndex < 0)
            {
                return true;
            }

            if (currentIndex >= _entries.Count)
            {
                currentIndex = -1;
                return false;
            }

            var currentEntry = _entries[currentIndex];
            return currentEntry.CanDepart(_trainUnit);
        }

        public RailNode GetNextDestination()
        {
            if (currentIndex < 0 || currentIndex >= _entries.Count)
            {
                return null;
            }

            var entry = _entries[currentIndex];
            return entry.IsValid ? entry.Node : null;
        }

        public void MoveToNextEntry()
        {
            if (!HasUsableEntry())
            {
                currentIndex = -1;
                return;
            }

            for (var i = 0; i < _entries.Count; i++)
            {
                currentIndex = (currentIndex + 1) % _entries.Count;
                if (_entries[currentIndex].IsValid)
                {
                    return;
                }
            }

            currentIndex = -1;
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

                if (currentIndex >= 0 && i <= currentIndex)
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
                currentIndex = -1;
                return;
            }

            if (removedBeforeCurrent > 0)
            {
                currentIndex -= removedBeforeCurrent;
            }

            if (currentIndex >= _entries.Count)
            {
                currentIndex = _entries.Count - 1;
            }

            if (currentIndex < 0 || !_entries[currentIndex].IsValid)
            {
                currentIndex = -1;
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

        public sealed class DiagramEntry
        {
            public DiagramEntry(RailNode node)
            {
                Node = node;
            }

            public RailNode Node { get; private set; }

            public bool IsValid => Node != null;

            public bool MatchesNode(RailNode node)
            {
                return Node == node;
            }

            public bool CanDepart(TrainUnit trainUnit)
            {
                var hasDockedCar = false;

                foreach (var car in trainUnit._cars)
                {
                    if (!car.IsDocked)
                    {
                        continue;
                    }

                    hasDockedCar = true;

                    if (!car.IsInventoryFull())
                    {
                        return false;
                    }
                }

                return hasDockedCar;
            }
        }
    }
}
