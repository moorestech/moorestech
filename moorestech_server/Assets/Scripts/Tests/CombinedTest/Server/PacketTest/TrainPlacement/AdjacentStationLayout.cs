using System;
using Core.Master;
using Game.Block.Interface;
using Game.Train.RailGraph;
using Game.Train.Unit;
using NUnit.Framework;
using Tests.Module.TestMod;
using Tests.Util;
using UnityEngine;

namespace Tests.CombinedTest.Server.PacketTest.TrainPlacement
{
    // 隣接駅2つを置き、自動接続された距離0の辺の両端と各駅の内部辺を名前で引ける fixture
    // Fixture that places two adjacent stations and names both ends of the auto-linked zero-length edge and each station's internal edge
    internal readonly struct AdjacentStationLayout
    {
        public AdjacentStationLayout(TrainTestEnvironment environment, RailNode firstEntry, RailNode firstExit, RailNode secondEntry, RailNode secondExit, int secondInternalLength, int trainLength, Guid trainCarGuid)
        {
            Environment = environment;
            FirstEntry = firstEntry;
            FirstExit = firstExit;
            SecondEntry = secondEntry;
            SecondExit = secondExit;
            SecondInternalLength = secondInternalLength;
            TrainLength = trainLength;
            TrainCarGuid = trainCarGuid;
        }

        public TrainTestEnvironment Environment { get; }
        public RailNode FirstEntry { get; }
        public RailNode FirstExit { get; }
        public RailNode SecondEntry { get; }
        public RailNode SecondExit { get; }
        public int SecondInternalLength { get; }
        public int TrainLength { get; }
        public Guid TrainCarGuid { get; }

        internal static AdjacentStationLayout Setup()
        {
            // 駅を2つ進行方向に隣接させ、自動接続された距離0の辺の両端を探す
            // Place two stations end to end and locate both ends of the auto-linked zero-length edge
            var environment = TrainTestHelper.CreateEnvironment();
            var stationLength = MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.TestTrainStation).BlockSize.z;
            var (_, first) = TrainTestHelper.PlaceBlockWithRailComponents(environment, ForUnitTestModBlockId.TestTrainStation, Vector3Int.zero, BlockDirection.East);
            var (_, second) = TrainTestHelper.PlaceBlockWithRailComponents(environment, ForUnitTestModBlockId.TestTrainStation, new Vector3Int(stationLength, 0, 0), BlockDirection.East);
            var graph = environment.GetRailGraphDatastore();
            var firstNodes = new[] { first[0].FrontNode, first[0].BackNode, first[1].FrontNode, first[1].BackNode };
            var secondNodes = new[] { second[0].FrontNode, second[0].BackNode, second[1].FrontNode, second[1].BackNode };

            var (firstExit, secondEntry) = FindZeroLengthLink();
            var (firstEntry, _) = FindInternalNeighbour(firstNodes, firstExit, true);
            var (secondExit, secondInternalLength) = FindInternalNeighbour(secondNodes, secondEntry, false);

            var trainCarMaster = MasterHolder.TrainUnitMaster.Train.TrainCars[0];
            var trainLength = TrainLengthConverter.ToRailUnits(trainCarMaster.Length);
            Assert.AreEqual(secondInternalLength, trainLength, "テスト用マスタでは駅内部長と車両長が一致する前提 / The test master pairs the station length with the car length");
            return new AdjacentStationLayout(environment, firstEntry, firstExit, secondEntry, secondExit, secondInternalLength, trainLength, trainCarMaster.TrainCarGuid);

            #region Internal

            (RailNode from, RailNode to) FindZeroLengthLink()
            {
                foreach (var a in firstNodes)
                foreach (var b in secondNodes)
                    if (graph.GetDistanceBetweenNodes(a, b) == 0) return (a, b);
                Assert.Fail("隣接駅の距離0接続が見つからない / Zero-length link between adjacent stations not found");
                return (null, null);
            }

            // anchorへ向かう辺(towardAnchor)か、anchorから出る辺かで駅内部の相手ノードを探す
            // Find the internal partner node by the edge into the anchor (towardAnchor) or out of it
            (RailNode node, int distance) FindInternalNeighbour(RailNode[] candidates, RailNode anchor, bool towardAnchor)
            {
                foreach (var node in candidates)
                {
                    var distance = towardAnchor ? graph.GetDistanceBetweenNodes(node, anchor) : graph.GetDistanceBetweenNodes(anchor, node);
                    if (0 < distance) return (node, distance);
                }
                Assert.Fail("駅内部の辺が見つからない / Station internal edge not found");
                return (null, -1);
            }

            #endregion
        }
    }
}
