using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using static Tests.UnitTest.Core.BeltTransport.BeltConveyorSegmentTestUtil;
using static Tests.UnitTest.Core.BeltTransport.Simulation.BeltSimulationTestUtil;

namespace Tests.UnitTest.Core.BeltTransport.Simulation.RandomNetwork
{
    // 網全体の不変条件を公開APIだけで検査し、決定論比較用の状態文字列を返す
    // Checks network-wide invariants through the public API only and returns a state string for determinism checks
    public static class RandomBeltNetworkInspector
    {
        public static string AssertInvariants(RandomBeltNetwork network, string context)
        {
            var seen = new HashSet<long>();
            var snapshot = new StringBuilder();
            for (var s = 0; s < network.Segments.Count; s++)
            {
                // segment内は出口から0以上・W以上の間隔・Length未満、個数は容量以下
                // Within a segment: from 0, at least W apart, below Length, and count within capacity
                var segment = network.Segments[s];
                var states = segment.CaptureItems();
                var where = $"{context} segment[{s}] {segment.Kind}";
                Assert.AreEqual(segment.Count, states.Length, $"{where} count");
                Assert.LessOrEqual(segment.Count, segment.Capacity, $"{where} capacity");
                for (var i = 0; i < states.Length; i++)
                {
                    var distance = states[i].DistanceToExit;
                    if (i == 0) Assert.GreaterOrEqual(distance, 0, $"{where} head distance");
                    else Assert.GreaterOrEqual(distance - states[i - 1].DistanceToExit, W, $"{where} spacing at {i}");
                    Assert.Less(distance, segment.Capacity * W, $"{where} distance below length at {i}");
                    AddOnce(Serial(states[i].Item), where);
                    snapshot.Append(Serial(states[i].Item)).Append('@').Append(distance).Append(' ');
                }

                // bufferの保持と優先順も比較対象に含める
                // Include buffer contents and priority order in the comparison
                if (segment.Buffer != null && segment.Buffer.TryGetItem(out var held))
                {
                    AddOnce(Serial(held), $"{where} buffer");
                    snapshot.Append("buf#").Append(Serial(held)).Append(' ');
                }
                snapshot.Append("order").Append(segment.PriorityOrder).Append('|');
            }

            // 機械へ届いた分も合わせ、投入した全アイテムがちょうど1回ずつ存在する
            // Including deliveries to machines, every inserted item exists exactly once
            foreach (var sink in network.Sinks)
            {
                foreach (var item in sink.Delivered) AddOnce(Serial(item), $"{context} sink");
                snapshot.Append("sink").Append(sink.Delivered.Count).Append('|');
            }
            Assert.AreEqual(network.InsertedSerials.Count, seen.Count, $"{context} conservation");
            Assert.IsTrue(seen.SetEquals(network.InsertedSerials), $"{context} item ids");
            return snapshot.ToString();

            #region Internal

            void AddOnce(long serial, string location)
            {
                Assert.IsTrue(seen.Add(serial), $"{location} duplicated item #{serial}");
            }

            #endregion
        }
    }
}
