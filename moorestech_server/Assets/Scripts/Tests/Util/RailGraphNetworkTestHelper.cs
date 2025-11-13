using System.Collections.Generic;
using System.Diagnostics;
using Game.Block.Blocks.TrainRail;
using Game.Train.RailGraph;
using NUnit.Framework;

namespace Tests.Util
{
    /// <summary>
    /// レールグラフのネットワーク構造を検証するためのスナップショットを取得・比較するヘルパー。
    /// </summary>
    public static class RailGraphNetworkTestHelper
    {
        public static RailGraphNetworkSnapshot CaptureFromComponents(IEnumerable<RailComponent> components)
        {
            Assert.IsNotNull(components, "RailComponentの列挙がnullです。");

            var adjacency = new Dictionary<ConnectionDestination, HashSet<RailGraphEdge>>();

            foreach (var component in components)
            {
                Assert.IsNotNull(component, "RailComponentがnullです。");

                CaptureNode(component.FrontNode, adjacency);
                CaptureNode(component.BackNode, adjacency);
            }

            return new RailGraphNetworkSnapshot(adjacency);
        }

        public static void AssertEquivalent(RailGraphNetworkSnapshot expected, RailGraphNetworkSnapshot actual)
        {
            Assert.IsNotNull(expected, "期待するRailGraphNetworkSnapshotがnullです。");
            Assert.IsNotNull(actual, "比較対象のRailGraphNetworkSnapshotがnullです。");

            Assert.AreEqual(expected.Adjacency.Count, actual.Adjacency.Count,
                "RailGraphに含まれるノード数が一致しません。");

            /*
            foreach (var (expectedNode, expectedEdges) in expected.Adjacency)
            {
                foreach (var expectedEdge in expectedEdges)
                {
                    UnityEngine.Debug.Log($"ノード {Describe(expectedNode)} が接続する {Describe(expectedEdge.Destination)} (距離: {expectedEdge.Distance}) ");
                }
            }

            //actualEdges
            foreach (var (actualNode, actualEdges) in actual.Adjacency)
            {
                foreach (var actualEdge in actualEdges)
                {
                    UnityEngine.Debug.Log($"ノード {Describe(actualNode)} が接続する {Describe(actualEdge.Destination)} (距離: {actualEdge.Distance}) ");
                }
            }
            */

            foreach (var (expectedNode, expectedEdges) in expected.Adjacency)
            {
                Assert.IsTrue(actual.Adjacency.TryGetValue(expectedNode, out var actualEdges),
                    $"ノード {Describe(expectedNode)} がロード後のグラフに存在しません。");

                Assert.AreEqual(expectedEdges.Count, actualEdges.Count,
                    $"ノード {Describe(expectedNode)} の接続数が一致しません。");

                foreach (var expectedEdge in expectedEdges)
                {
                    Assert.IsTrue(actualEdges.Contains(expectedEdge), $"ノード {Describe(expectedNode)} が接続する {Describe(expectedEdge.Destination)} (距離: {expectedEdge.Distance}) がロード後に見つかりません。");
                }
            }

            
            
        }

        private static void CaptureNode(RailNode node, Dictionary<ConnectionDestination, HashSet<RailGraphEdge>> adjacency)
        {
            Assert.IsNotNull(node, "RailNodeがnullです。");
            Assert.IsTrue(RailGraphDatastore.TryGetConnectionDestination(node, out var destination) && destination != null,
                "RailNodeにConnectionDestinationが割り当てられていません。");

            var nodeKey = Clone(destination);
            if (!adjacency.TryGetValue(nodeKey, out var edges))
            {
                edges = new HashSet<RailGraphEdge>();
                adjacency[nodeKey] = edges;
            }

            foreach (var (neighbor, distance) in node.ConnectedNodesWithDistance)
            {
                Assert.IsTrue(RailGraphDatastore.TryGetConnectionDestination(neighbor, out var neighborDestination) && neighborDestination != null,
                    "接続先のRailNodeにConnectionDestinationが割り当てられていません。");

                edges.Add(new RailGraphEdge(Clone(neighborDestination), distance));
            }
        }

        private static ConnectionDestination Clone(ConnectionDestination destination)
        {
            Assert.IsNotNull(destination, "ConnectionDestinationがnullです。");
            var originalId = destination.railComponentID;
            var copiedId = new RailComponentID(originalId.Position, originalId.ID);
            return new ConnectionDestination(copiedId, destination.IsFront);
        }

        private static string Describe(ConnectionDestination destination)
        {
            var id = destination.railComponentID;
            return $"({id.Position.x}, {id.Position.y}, {id.Position.z}) {(destination.IsFront ? "Front" : "Back")}";
        }
    }

    public sealed class RailGraphNetworkSnapshot
    {
        internal RailGraphNetworkSnapshot(Dictionary<ConnectionDestination, HashSet<RailGraphEdge>> adjacency)
        {
            Adjacency = adjacency;
        }

        internal Dictionary<ConnectionDestination, HashSet<RailGraphEdge>> Adjacency { get; }
    }

    internal readonly struct RailGraphEdge : System.IEquatable<RailGraphEdge>
    {
        public RailGraphEdge(ConnectionDestination destination, int distance)
        {
            Destination = destination;
            Distance = distance;
        }

        public ConnectionDestination Destination { get; }
        public int Distance { get; }

        public bool Equals(RailGraphEdge other)
        {
            return Distance == other.Distance && Equals(Destination, other.Destination);
        }

        public override bool Equals(object obj)
        {
            return obj is RailGraphEdge other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return ((Destination != null ? Destination.GetHashCode() : 0) * 397) ^ Distance;
            }
        }
    }
}
