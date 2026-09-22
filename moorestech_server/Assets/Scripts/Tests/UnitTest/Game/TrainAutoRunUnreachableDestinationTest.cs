using System.Collections.Generic;
using Game.Block.Interface;
using Game.Train.Diagram;
using Game.Train.RailGraph;
using Game.Train.RailPositions;
using Game.Train.Unit;
using NUnit.Framework;
using Tests.Util;
using UnityEngine;

namespace Tests.UnitTest.Game
{
    // 順方向・逆方向ともに経路が無い目的地で自動運転をONにしたときの回帰テスト。
    // Regression test for turning auto-run on with a destination unreachable in both directions.
    public class TrainAutoRunUnreachableDestinationTest
    {
        [Test]
        public void TurnOnAutoRun_WithUnreachableDestination_TurnsOffWithoutException()
        {
            var env = TrainTestHelper.CreateEnvironment();
            _ = env.GetRailGraphDatastore();

            // 列車が走る区間を双方向に接続する。
            // Connect the segment the train sits on in both directions.
            var railA = TrainTestHelper.PlaceRail(env, new Vector3Int(0, 0, 0), BlockDirection.North);
            var railB = TrainTestHelper.PlaceRail(env, new Vector3Int(100, 0, 0), BlockDirection.North);
            railB.FrontNode.ConnectNode(railA.FrontNode);
            railA.BackNode.ConnectNode(railB.BackNode);

            // グラフから孤立したレールを目的地にする。
            // Use a rail isolated from the graph as the destination.
            var isolatedRail = TrainTestHelper.PlaceRail(env, new Vector3Int(9999, 0, 9999), BlockDirection.North);

            var nodeList = new List<IRailNode> { railA.FrontNode, railB.FrontNode };
            var trainLength = 10;
            var railPosition = new RailPosition(nodeList, trainLength, 5);

            var cars = new List<TrainCar>
            {
                TrainTestCarFactory.CreateTrainCarWithItemContainer(0, 400000, 0, trainLength, true,
                    TrainTestCarFactory.StableAutoRunTestWeight).trainCar,
            };
            var trainUnit = new TrainUnit(railPosition, cars, env.GetTrainRailPositionManager(), env.GetTrainDiagramManager());
            trainUnit.trainDiagram.AddEntry(isolatedRail.FrontNode);

            // 到達不能な目的地でもNullReferenceにならず、自動運転が解除されること。
            // Auto-run must switch off instead of throwing a NullReferenceException.
            Assert.DoesNotThrow(() => trainUnit.TurnOnAutoRun(),
                "到達不能な目的地で自動運転をONにすると例外が発生しています。");
            Assert.IsFalse(trainUnit.IsAutoRun,
                "到達不能な目的地なので自動運転は解除されているはずです。");
        }
    }
}
