using System.Collections.Generic;
using Game.Train.RailGraph;
using Game.Train.RailPositions;
using NUnit.Framework;
using Tests.Util;

namespace Tests.UnitTest.Game
{
    public class RailPositionRouteDistanceFinderTest
    {
        [Test]
        public void FindShortestDistance_WhenInputContainsNull_ReturnsMinusOne()
        {
            // 日本語: null入力は探索不能として-1を返す。
            // English: Null input should be treated as non-routable and return -1.
            Assert.AreEqual(-1, RailPositionRouteDistanceFinder.FindShortestDistance(null, null));
        }

        [Test]
        public void FindShortestDistance_WhenPositionsAreSame_ReturnsZero()
        {
            // 日本語: 同一位置は経路長0として扱う。
            // English: Identical positions should produce zero distance.
            var env = TrainTestHelper.CreateEnvironment();
            var graph = env.GetRailGraphDatastore();
            var approaching = RailNode.CreateSingleAndRegister(graph);
            var justPassed = RailNode.CreateSingleAndRegister(graph);
            justPassed.ConnectNode(approaching, 10);

            var start = new RailPosition(new List<IRailNode> { approaching, justPassed }, 0, 4);
            var end = new RailPosition(new List<IRailNode> { approaching, justPassed }, 0, 4);

            var distance = RailPositionRouteDistanceFinder.FindShortestDistance(start, end);

            Assert.AreEqual(0, distance);
        }

        [Test]
        public void FindShortestDistance_WhenSameSegmentForward_ReturnsDirectDistance()
        {
            // 日本語: 同一セグメント上で前方向に終点がある場合は差分距離で到達できる。
            // English: On the same segment, forward-reachable end should use direct delta distance.
            var env = TrainTestHelper.CreateEnvironment();
            var graph = env.GetRailGraphDatastore();
            var approaching = RailNode.CreateSingleAndRegister(graph);
            var justPassed = RailNode.CreateSingleAndRegister(graph);
            justPassed.ConnectNode(approaching, 10);

            var start = new RailPosition(new List<IRailNode> { approaching, justPassed }, 0, 7);
            var end = new RailPosition(new List<IRailNode> { approaching, justPassed }, 0, 3);

            var distance = RailPositionRouteDistanceFinder.FindShortestDistance(start, end);

            Assert.AreEqual(4, distance);
        }

        [Test]
        public void FindShortestDistance_WhenSameSegmentBackwardTarget_UsesDetourPath()
        {
            // 日本語: 同一セグメントでも終点が後方ならノード経由の迂回経路を選ぶ。
            // English: If target is behind on same segment, routed detour path should be selected.
            var env = TrainTestHelper.CreateEnvironment();
            var graph = env.GetRailGraphDatastore();
            var approaching = RailNode.CreateSingleAndRegister(graph);
            var justPassed = RailNode.CreateSingleAndRegister(graph);
            var relay = RailNode.CreateSingleAndRegister(graph);
            justPassed.ConnectNode(approaching, 10);
            approaching.ConnectNode(relay, 2);
            relay.ConnectNode(justPassed, 2);

            var start = new RailPosition(new List<IRailNode> { approaching, justPassed }, 0, 3);
            var end = new RailPosition(new List<IRailNode> { approaching, justPassed }, 0, 7);

            var distance = RailPositionRouteDistanceFinder.FindShortestDistance(start, end);

            Assert.AreEqual(10, distance);
        }

        [Test]
        public void FindShortestDistance_WhenMultiSegmentPathExists_ReturnsAccumulatedDistance()
        {
            // 日本語: セグメント跨ぎの探索では始終点オフセットとノード経路距離を合算する。
            // English: Multi-segment path distance should include endpoint offsets plus node path cost.
            var env = TrainTestHelper.CreateEnvironment();
            var graph = env.GetRailGraphDatastore();
            var startApproaching = RailNode.CreateSingleAndRegister(graph);
            var startJustPassed = RailNode.CreateSingleAndRegister(graph);
            var middle = RailNode.CreateSingleAndRegister(graph);
            var endJustPassed = RailNode.CreateSingleAndRegister(graph);
            var endApproaching = RailNode.CreateSingleAndRegister(graph);
            startJustPassed.ConnectNode(startApproaching, 10);
            startApproaching.ConnectNode(middle, 20);
            middle.ConnectNode(endJustPassed, 5);
            endJustPassed.ConnectNode(endApproaching, 8);

            var start = new RailPosition(new List<IRailNode> { startApproaching, startJustPassed }, 0, 6);
            var end = new RailPosition(new List<IRailNode> { endApproaching, endJustPassed }, 0, 3);

            var distance = RailPositionRouteDistanceFinder.FindShortestDistance(start, end);

            Assert.AreEqual(36, distance);
        }

        [Test]
        public void FindShortestDistance_WhenNoPathExists_ReturnsMinusOne()
        {
            // 日本語: ノード経路が未到達なら-1を返し、0距離誤判定を防ぐ。
            // English: Unreachable node path should return -1 and avoid false zero-distance.
            var env = TrainTestHelper.CreateEnvironment();
            var graph = env.GetRailGraphDatastore();
            var startApproaching = RailNode.CreateSingleAndRegister(graph);
            var startJustPassed = RailNode.CreateSingleAndRegister(graph);
            var endApproaching = RailNode.CreateSingleAndRegister(graph);
            var endJustPassed = RailNode.CreateSingleAndRegister(graph);
            startJustPassed.ConnectNode(startApproaching, 10);
            endJustPassed.ConnectNode(endApproaching, 6);

            var start = new RailPosition(new List<IRailNode> { startApproaching, startJustPassed }, 0, 4);
            var end = new RailPosition(new List<IRailNode> { endApproaching, endJustPassed }, 0, 2);

            var distance = RailPositionRouteDistanceFinder.FindShortestDistance(start, end);

            Assert.AreEqual(-1, distance);
        }
    }
}
