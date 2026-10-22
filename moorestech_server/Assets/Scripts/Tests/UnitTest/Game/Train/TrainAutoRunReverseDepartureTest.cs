using System.Collections.Generic;
using System.Linq;
using Game.Block.Blocks.TrainRail;
using Game.Block.Interface;
using Game.Train.Diagram;
using Game.Train.RailCalc;
using Game.Train.RailGraph;
using Game.Train.RailPositions;
using Game.Train.Unit;
using NUnit.Framework;
using Tests.Module.TestMod;
using Tests.Util;
using UnityEngine;

namespace Tests.UnitTest.Game
{
    // 行き止まりの駅で反転して次の駅へ向かう自動運転の回帰テスト
    // Regression tests for auto-run reversing at a dead-end station toward the next station
    //
    // 配置: [駅A]==R==[駅B] の一本線。駅Aの奥(Back側出口)と駅Bの奥(Front側出口)が行き止まり。
    // 列車は駅Aに奥向きで停車し、後部車両は駅を出て区間Rへ5マス（5×RAIL_LENGTH_SCALE）はみ出す。反転後の先頭はノード途中になる。
    // Layout: a single line [A]==R==[B]; A's back exit and B's front exit are dead ends.
    // The train stops at A facing the dead end with its rear wagon 5 master units (5×RAIL_LENGTH_SCALE) out into R, so after reversing the head sits mid-edge.
    public class TrainAutoRunReverseDepartureTest
    {
        private const int TransitSegmentLength = 20000;
        // 車両長はマスタ単位（×RAIL_LENGTH_SCALE でレール単位になる）
        // Car lengths are in master units (multiplied by RAIL_LENGTH_SCALE into rail units)
        private const int RearWagonMasterLength = 5;

        [Test]
        public void 駅で反転した列車は残距離を引き直して次の駅まで走る()
        {
            var (trainUnit, stationA, stationB) = CreateShuttle();
            trainUnit.trainDiagram.AddEntry(stationA.BackExit, TrainDiagram.DepartureConditionType.WaitForTicks, 1);
            trainUnit.trainDiagram.AddEntry(stationB.FrontExit, TrainDiagram.DepartureConditionType.WaitForTicks, 100000);
            trainUnit.TurnOnAutoRun();
            trainUnit.Update();
            Assert.IsTrue(trainUnit.IsDocked, "列車が駅Aに停車した状態で始まっていません。");

            // 旧実装は反転後の残距離が0付近のまま残り、約0.07m/sで這って数千tickかかっていた
            // The old code kept a near-zero remaining distance after reversing and crawled at ~0.07 m/s for thousands of ticks
            for (var tick = 0; tick < 2000; tick++)
            {
                trainUnit.Update();
                Assert.IsTrue(trainUnit.IsAutoRun, $"tick={tick} で自動運転が切れました。");
                if (IsStoppedAt(trainUnit, stationB.FrontExit)) return;
            }
            Assert.Fail("反転後に2000tick以内で駅Bへ着きませんでした。残距離が引き直されていない可能性があります。");
        }

        [Test]
        public void 反転しても着けない目的地は発車時にその場で自動運転を切る()
        {
            var (trainUnit, stationA, stationB) = CreateShuttle();
            trainUnit.trainDiagram.AddEntry(stationA.BackExit, TrainDiagram.DepartureConditionType.WaitForTicks, 1);
            // 駅Bの奥向き出口はAから見てどちら向きにも着けない
            // B's back exit is unreachable from A in either direction
            trainUnit.trainDiagram.AddEntry(stationB.BackExit, TrainDiagram.DepartureConditionType.WaitForTicks, 1);
            trainUnit.TurnOnAutoRun();
            trainUnit.Update();
            Assert.IsTrue(trainUnit.IsDocked, "列車が駅Aに停車した状態で始まっていません。");

            for (var tick = 0; tick < 10 && trainUnit.IsAutoRun; tick++)
            {
                trainUnit.Update();
            }

            // 旧実装は確かめずに反転して走り出し、途中のノードで自動運転が切れてクライアントとずれていた
            // The old code reversed blindly, drove off, and stopped auto-run mid-route out of sync with the client
            Assert.IsFalse(trainUnit.IsAutoRun, "着けない目的地なのに自動運転が続いています。");
            Assert.IsTrue(IsStoppedAt(trainUnit, stationA.BackExit), "反転したまま走り出しています。駅Aの向きのまま止まるべきです。");
        }

        #region Helpers

        private static (TrainUnit trainUnit, StationExits stationA, StationExits stationB) CreateShuttle()
        {
            var env = TrainTestHelper.CreateEnvironment();
            var (_, stationAComponents) = TrainTestHelper.PlaceBlockWithRailComponents(env, ForUnitTestModBlockId.TestTrainItemPlatform, new Vector3Int(0, 0, 0), BlockDirection.North);
            var (_, stationBComponents) = TrainTestHelper.PlaceBlockWithRailComponents(env, ForUnitTestModBlockId.TestTrainItemPlatform, new Vector3Int(0, 0, 40), BlockDirection.North);
            var stationA = StationExits.From(stationAComponents);
            var stationB = StationExits.From(stationBComponents);
            var transit = TrainTestHelper.PlaceRail(env, new Vector3Int(0, 0, 20), BlockDirection.North);

            // A出口 → R → B入口を双方向に結び、両端は行き止まりのままにする
            // Link A's exit → R → B's entry both ways and leave both ends dead
            stationA.ExitComponent.FrontNode.ConnectNode(transit.FrontNode, TransitSegmentLength);
            transit.BackNode.ConnectNode(stationA.ExitComponent.BackNode, TransitSegmentLength);
            transit.FrontNode.ConnectNode(stationB.EntryComponent.FrontNode, TransitSegmentLength);
            stationB.EntryComponent.BackNode.ConnectNode(transit.BackNode, TransitSegmentLength);

            // 先頭はAの奥向き出口、機関車は駅内、後部貨車は区間Rへはみ出す
            // Head at A's back exit with the locomotive inside the station and the rear wagon sticking out into R
            var locomotive = TrainTestCarFactory.CreateTrainCarWithItemContainer(0, 400000, 1, StationMasterLength(stationA), true, TrainTestCarFactory.StableAutoRunTestWeight).trainCar;
            var wagon = TrainTestCarFactory.CreateTrainCarWithItemContainer(0, 0, 1, RearWagonMasterLength, true, TrainTestCarFactory.StableAutoRunTestWeight).trainCar;
            var nodes = new List<IRailNode> { stationA.BackExit, stationA.ExitComponent.BackNode, transit.BackNode };
            var railPosition = new RailPosition(nodes, locomotive.Length + wagon.Length, 0);
            var trainUnit = new TrainUnit(railPosition, new List<TrainCar> { locomotive, wagon }, env.GetTrainRailPositionManager(), env.GetTrainDiagramManager());
            return (trainUnit, stationA, stationB);
        }

        private static int StationMasterLength(StationExits station)
        {
            var railUnits = station.ExitComponent.BackNode.GetDistanceToNode(station.BackExit);
            var scale = (int)BezierUtility.RAIL_LENGTH_SCALE;
            Assert.AreEqual(0, railUnits % scale, "駅の内部長がマスタ単位の整数倍になっていません。");
            return railUnits / scale;
        }

        private static bool IsStoppedAt(TrainUnit trainUnit, IRailNode node)
        {
            return trainUnit.RailPosition.GetNodeApproaching() == node && trainUnit.RailPosition.GetDistanceToNextNode() == 0;
        }

        private readonly struct StationExits
        {
            public RailComponent EntryComponent { get; }
            public RailComponent ExitComponent { get; }
            public RailNode FrontExit => ExitComponent.FrontNode;
            public RailNode BackExit => EntryComponent.BackNode;

            private StationExits(RailComponent entryComponent, RailComponent exitComponent)
            {
                EntryComponent = entryComponent;
                ExitComponent = exitComponent;
            }

            public static StationExits From(IReadOnlyList<RailComponent> components)
            {
                var entry = components.First(component => component.FrontNode.StationRef.NodeRole == StationNodeRole.Entry);
                var exit = components.First(component => component.FrontNode.StationRef.NodeRole == StationNodeRole.Exit);
                return new StationExits(entry, exit);
            }
        }

        #endregion
    }
}
