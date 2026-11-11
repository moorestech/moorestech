using System.Collections.Generic;
using Client.Ui.State.Train.Logic;
using Client.Ui.State.Train.Timetable;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.Ui.State.Train.Timetable
{
    public class TrainTimetableStationCatalogTest
    {
        [Test]
        public void 駅はxzyの順で並ぶ()
        {
            // Web ホストの SortStations と同じ優先順
            // Same priority as the Web host's SortStations
            var sorted = TrainTimetableStationCatalog.Sorted(new List<TrainTimetableStation>
            {
                new(new Vector3Int(1, 5, 0), "b"),
                new(new Vector3Int(0, 9, 9), "a"),
                new(new Vector3Int(1, 0, 0), "c"),
                new(new Vector3Int(1, 9, -1), "d"),
            });
            CollectionAssert.AreEqual(new[] { "a", "d", "c", "b" }, sorted.ConvertAll(s => s.Name));
        }
    }
}
