using Core.Master;
using Game.Block.Blocks.TrainRail;
using Game.Block.Interface;
using Game.Train.RailGraph;
using Game.Train.RailPosition;
using Game.Train.Unit;
using NUnit.Framework;
using System.Collections.Generic;
using System.Linq;
using Tests.Util;
using UnityEngine;
using Tests.Module.TestMod;


namespace Tests.UnitTest.Game
{
    public class TrainUnitAddCarTest
    {

        private static StationNodeSet ExtractStationNodes(IBlock stationBlock, RailSaverComponent stationSaver)
        {
            var entryComponent = stationSaver.RailComponents
                .FirstOrDefault(component =>
                    component.FrontNode.StationRef.NodeRole == StationNodeRole.Entry &&
                    component.FrontNode.StationRef.NodeSide == StationNodeSide.Front);
            Assert.IsNotNull(entryComponent, "駅の正面Entryノードを持つRailComponentが見つかりません。");

            var exitComponent = stationSaver.RailComponents
                .FirstOrDefault(component =>
                    component.FrontNode.StationRef.NodeRole == StationNodeRole.Exit &&
                    component.FrontNode.StationRef.NodeSide == StationNodeSide.Front);
            Assert.IsNotNull(exitComponent, "駅の正面Exitノードを持つRailComponentが見つかりません。");

            var entryFront = entryComponent!.FrontNode;
            var exitFront = exitComponent!.FrontNode;

            var entryBack = exitComponent.BackNode;
            Assert.AreEqual(StationNodeRole.Entry, entryBack.StationRef.NodeRole,
                "Exit側RailComponentの背面ノードがEntryとして設定されていません。");
            Assert.AreEqual(StationNodeSide.Back, entryBack.StationRef.NodeSide,
                "Exit側RailComponentの背面ノードがBack側として設定されていません。");

            var exitBack = entryComponent.BackNode;
            Assert.AreEqual(StationNodeRole.Exit, exitBack.StationRef.NodeRole,
                "Entry側RailComponentの背面ノードがExitとして設定されていません。");
            Assert.AreEqual(StationNodeSide.Back, exitBack.StationRef.NodeSide,
                "Entry側RailComponentの背面ノードがBack側として設定されていません。");

            var segmentLength = entryFront.GetDistanceToNode(exitFront);
            Assert.Greater(segmentLength, 0, "駅セグメントの長さが0以下になっています。");
            var blockLength = stationBlock.BlockPositionInfo.BlockSize.z;
            Assert.Greater(blockLength, 0, "駅ブロックのZサイズが0以下になっています。");

            return new StationNodeSet(entryComponent!, exitComponent!, exitFront, entryFront, exitBack, entryBack, segmentLength, blockLength);
        }


        //rearにcarをつける
        //①rearの位置はnodeとnodeの間
        //②追加carはnodeをまたがない
        //前後関係チェック、長さチェック、autorun耐性チェック(未)、セーブロード後のバリデーションチェック(未)
        [Test]
        public void AddCar1()
        {
            var environment = TrainTestHelper.CreateEnvironment();
            var railA = TrainTestHelper.PlaceRail(environment, new Vector3Int(0, 0, 0), BlockDirection.North);
            var railB = TrainTestHelper.PlaceRail(environment, new Vector3Int(1100, 0, 0), BlockDirection.North);
            railB.ConnectRailComponent(railA, true, true);

            var firstTrain = MasterHolder.TrainUnitMaster.Train.TrainCars.First();

            var cars = new List<TrainCar>
            {
                new TrainCar(firstTrain,true),
                new TrainCar(firstTrain,true),
                new TrainCar(firstTrain,true),
                new TrainCar(firstTrain,true)
            };

            var railPosition = new RailPosition(new List<IRailNode> { railA.FrontNode, railB.FrontNode }, firstTrain.Length * 4, firstTrain.Length * 3 / 2);
            var trainUnit = new TrainUnit(railPosition, cars, environment.GetTrainUpdateService(), environment.GetTrainRailPositionManager(), environment.GetTrainDiagramManager());

            //aproachingがrailA.FrontNode方向
            Assert.AreEqual(trainUnit.RailPosition.GetNodeApproaching(), railA.FrontNode, "approachingが不正");
            Assert.AreEqual(trainUnit.RailPosition.GetDistanceToNextNode(), firstTrain.Length * 3 / 2, "distanceToNextNodeが不正");
            Assert.AreEqual(trainUnit.Cars.Count, 4, "4両でない");
            Assert.AreEqual(trainUnit.RailPosition.TrainLength, firstTrain.Length * 4, "編成長さが不正");
            

            foreach (var car in trainUnit.Cars)
            {
                Assert.IsTrue(car.IsFacingForward, "全車前向きでない");
            }

            var newCar = new TrainCar(firstTrain, true);
            var railPosition2 = new RailPosition(new List<IRailNode> { railA.FrontNode, railB.FrontNode }, firstTrain.Length, firstTrain.Length * 3 / 2 + firstTrain.Length * 4);
            trainUnit.AttachCarToRear(newCar, railPosition2);
            Assert.AreEqual(trainUnit.RailPosition.GetNodeApproaching(), railA.FrontNode, "追加approachingが不正");
            Assert.AreEqual(trainUnit.RailPosition.GetDistanceToNextNode(), firstTrain.Length * 3 / 2, "追加distanceToNextNodeが不正");
            Assert.AreEqual(trainUnit.Cars.Count, 5, "追加後5両でない");
            Assert.AreEqual(trainUnit.RailPosition.TrainLength, firstTrain.Length * 5, "追加後編成長さが不正");
            foreach (var car in trainUnit.Cars)
            {
                Assert.IsTrue(car.IsFacingForward, "追加後全車前向きでない");
            }
        }

        //headにcarをつける
        //①headの位置はnodeとnodeの間
        //②追加carはnodeをまたがない
        //前後関係チェック、長さチェック、autorun耐性チェック(未)、セーブロード後のバリデーションチェック(未)
        [Test]
        public void AddCar2()
        {
            var environment = TrainTestHelper.CreateEnvironment();
            var railA = TrainTestHelper.PlaceRail(environment, new Vector3Int(0, 0, 0), BlockDirection.North);
            var railB = TrainTestHelper.PlaceRail(environment, new Vector3Int(1100, 0, 0), BlockDirection.North);
            railB.ConnectRailComponent(railA, true, true);

            var firstTrain = MasterHolder.TrainUnitMaster.Train.TrainCars.First();

            var cars = new List<TrainCar>
            {
                new TrainCar(firstTrain,true),
                new TrainCar(firstTrain,true),
                new TrainCar(firstTrain,true),
                new TrainCar(firstTrain,true)
            };

            var railPosition = new RailPosition(new List<IRailNode> { railA.FrontNode, railB.FrontNode }, firstTrain.Length * 4, firstTrain.Length * 3 / 2);
            var trainUnit = new TrainUnit(railPosition, cars, environment.GetTrainUpdateService(), environment.GetTrainRailPositionManager(), environment.GetTrainDiagramManager());

            Assert.AreEqual(trainUnit.RailPosition.GetNodeApproaching(), railA.FrontNode, "approachingが不正");
            Assert.AreEqual(trainUnit.RailPosition.GetDistanceToNextNode(), firstTrain.Length * 3 / 2, "distanceToNextNodeが不正");
            Assert.AreEqual(trainUnit.Cars.Count, 4, "4両でない");
            Assert.AreEqual(trainUnit.RailPosition.TrainLength, firstTrain.Length * 4, "編成長さが不正");
            foreach (var car in trainUnit.Cars)
            {
                Assert.IsTrue(car.IsFacingForward, "全車前向きでない");
            }

            var newCar = new TrainCar(firstTrain, true);
            var railPosition2 = new RailPosition(new List<IRailNode> { railA.FrontNode, railB.FrontNode }, firstTrain.Length, firstTrain.Length * 3 / 2 - firstTrain.Length);
            trainUnit.AttachCarToHead(newCar, railPosition2);
            Assert.AreEqual(trainUnit.RailPosition.GetNodeApproaching(), railA.FrontNode, "追加approachingが不正");
            Assert.AreEqual(trainUnit.RailPosition.GetDistanceToNextNode(), firstTrain.Length * 3 / 2 - firstTrain.Length, "追加distanceToNextNodeが不正");
            Assert.AreEqual(trainUnit.Cars.Count, 5, "追加後5両でない");
            Assert.AreEqual(trainUnit.RailPosition.TrainLength, firstTrain.Length * 5, "追加後編成長さが不正");
            foreach (var car in trainUnit.Cars)
            {
                Assert.IsTrue(car.IsFacingForward, "追加後全車前向きでない");
            }
        }


        //rearにcarをつける
        //①rearの位置はnodeの真上
        //②追加carはnodeをまたがない
        //前後関係チェック、長さチェック、autorun耐性チェック(未)、セーブロード後のバリデーションチェック(未)
        [Test]
        public void AddCar3()
        {
            var environment = TrainTestHelper.CreateEnvironment();
            var railA = TrainTestHelper.PlaceRail(environment, new Vector3Int(0, 0, 0), BlockDirection.North);
            var railB = TrainTestHelper.PlaceRail(environment, new Vector3Int(1100, 0, 0), BlockDirection.North);
            var railC = TrainTestHelper.PlaceRail(environment, new Vector3Int(2100, 0, 0), BlockDirection.North);
            railB.ConnectRailComponent(railA, true, true);
            railC.ConnectRailComponent(railB, true, true);

            var firstTrain = MasterHolder.TrainUnitMaster.Train.TrainCars.First();
            var ablength = railB.FrontNode.GetDistanceToNode(railA.FrontNode);
            var cars = new List<TrainCar>
            {
                new TrainCar(firstTrain,true),
                new TrainCar(firstTrain,true),
                new TrainCar(firstTrain,true),
                new TrainCar(firstTrain,true)
            };

            var railPosition = new RailPosition(new List<IRailNode> { railA.FrontNode, railB.FrontNode }, firstTrain.Length * 4, ablength - firstTrain.Length * 4);
            var trainUnit = new TrainUnit(railPosition, cars, environment.GetTrainUpdateService(), environment.GetTrainRailPositionManager(), environment.GetTrainDiagramManager());

            //aproachingがrailA.FrontNode方向
            Assert.AreEqual(trainUnit.RailPosition.GetNodeApproaching(), railA.FrontNode, "approachingが不正");
            Assert.AreEqual(trainUnit.RailPosition.GetDistanceToNextNode(), ablength - firstTrain.Length * 4, "distanceToNextNodeが不正");
            Assert.AreEqual(trainUnit.Cars.Count, 4, "4両でない");
            Assert.AreEqual(trainUnit.RailPosition.TrainLength, firstTrain.Length * 4, "編成長さが不正");
            trainUnit.Reverse();
            Assert.AreEqual(trainUnit.RailPosition.GetNodeApproaching(), railB.BackNode, "approachingが不正");
            Assert.AreEqual(trainUnit.RailPosition.GetDistanceToNextNode(), 0, "distanceToNextNodeが不正");
            trainUnit.Reverse();
            var railPositionRear = trainUnit.RailPosition.GetRearRailPosition();
            Assert.AreEqual(1, railPositionRear.GetRailNodes().Count, "GetRearRailPositionが想定外");
            Assert.AreEqual(railPositionRear.GetNodeApproaching(), railB.FrontNode, "rear approachingが不正");

            foreach (var car in trainUnit.Cars)
            {
                Assert.IsTrue(car.IsFacingForward, "全車前向きでない");
            }

            var newCar = new TrainCar(firstTrain, true);
            var railPosition2 = new RailPosition(new List<IRailNode> { railB.FrontNode, railC.FrontNode }, firstTrain.Length, 0);
            
                
            trainUnit.AttachCarToRear(newCar, railPosition2);
            Assert.AreEqual(trainUnit.RailPosition.GetNodeApproaching(), railA.FrontNode, "追加approachingが不正");
            Assert.AreEqual(trainUnit.RailPosition.GetDistanceToNextNode(), ablength - firstTrain.Length * 4, "追加distanceToNextNodeが不正");
            Assert.AreEqual(trainUnit.Cars.Count, 5, "追加後5両でない");
            Assert.AreEqual(trainUnit.RailPosition.TrainLength, firstTrain.Length * 5, "追加後編成長さが不正");
            foreach (var car in trainUnit.Cars)
            {
                Assert.IsTrue(car.IsFacingForward, "追加後全車前向きでない");
            }
        }

        //headにcarをつける
        //①rearの位置はnodeの真上
        //②追加carはnodeをまたがない
        //前後関係チェック、長さチェック、autorun耐性チェック(未)、セーブロード後のバリデーションチェック(未)
        [Test]
        public void AddCar4()
        {
            var environment = TrainTestHelper.CreateEnvironment();
            var railA = TrainTestHelper.PlaceRail(environment, new Vector3Int(0, 0, 0), BlockDirection.North);
            var railB = TrainTestHelper.PlaceRail(environment, new Vector3Int(1100, 0, 0), BlockDirection.North);
            var railC = TrainTestHelper.PlaceRail(environment, new Vector3Int(2100, 0, 0), BlockDirection.North);
            railB.ConnectRailComponent(railA, true, true);
            railC.ConnectRailComponent(railB, true, true);

            var firstTrain = MasterHolder.TrainUnitMaster.Train.TrainCars.First();
            var cblength = railC.FrontNode.GetDistanceToNode(railB.FrontNode);
            var cars = new List<TrainCar>
            {
                new TrainCar(firstTrain,true),
                new TrainCar(firstTrain,true),
                new TrainCar(firstTrain,true),
                new TrainCar(firstTrain,true)
            };

            var railPosition = new RailPosition(new List<IRailNode> { railB.FrontNode, railC.FrontNode }, firstTrain.Length * 4, 0);
            var trainUnit = new TrainUnit(railPosition, cars, environment.GetTrainUpdateService(), environment.GetTrainRailPositionManager(), environment.GetTrainDiagramManager());

            Assert.AreEqual(trainUnit.RailPosition.GetNodeApproaching(), railB.FrontNode, "approachingが不正");
            Assert.AreEqual(trainUnit.RailPosition.GetDistanceToNextNode(), 0, "distanceToNextNodeが不正");
            Assert.AreEqual(trainUnit.Cars.Count, 4, "4両でない");
            Assert.AreEqual(trainUnit.RailPosition.TrainLength, firstTrain.Length * 4, "編成長さが不正");
            trainUnit.Reverse();
            Assert.AreEqual(trainUnit.RailPosition.GetNodeApproaching(), railC.BackNode, "approachingが不正");
            Assert.AreEqual(trainUnit.RailPosition.GetDistanceToNextNode(), cblength - firstTrain.Length * 4, "distanceToNextNodeが不正");
            trainUnit.Reverse();
            var railPositionRear = trainUnit.RailPosition.GetHeadRailPosition();
            Assert.AreEqual(1, railPositionRear.GetRailNodes().Count, "GetRearRailPositionが想定外");
            Assert.AreEqual(railPositionRear.GetNodeApproaching(), railB.FrontNode, "rear approachingが不正");

            foreach (var car in trainUnit.Cars)
            {
                Assert.IsTrue(car.IsFacingForward, "全車前向きでない");
            }
            var balength = railB.FrontNode.GetDistanceToNode(railA.FrontNode);
            var newCar = new TrainCar(firstTrain, true);
            var railPosition2 = new RailPosition(new List<IRailNode> { railA.FrontNode, railB.FrontNode }, firstTrain.Length, balength - firstTrain.Length);


            trainUnit.AttachCarToHead(newCar, railPosition2);
            Assert.AreEqual(3, trainUnit.RailPosition.GetRailNodes().Count, "GetRailNodes().Countが3じゃない");
            Assert.AreEqual(trainUnit.RailPosition.GetNodeApproaching(), railA.FrontNode, "追加approachingが不正");
            Assert.AreEqual(trainUnit.RailPosition.GetDistanceToNextNode(), balength - firstTrain.Length, "追加distanceToNextNodeが不正");
            Assert.AreEqual(trainUnit.Cars.Count, 5, "追加後5両でない");
            Assert.AreEqual(trainUnit.RailPosition.TrainLength, firstTrain.Length * 5, "追加後編成長さが不正");
            foreach (var car in trainUnit.Cars)
            {
                Assert.IsTrue(car.IsFacingForward, "追加後全車前向きでない");
            }
        }

        //rearにcarをつける
        //①rearの位置はnodeの真上、node距離0の状態あり
        //②追加carはnodeをまたがない
        //前後関係チェック、長さチェック、autorun耐性チェック(未)、セーブロード後のバリデーションチェック(未)
        [Test]
        public void AddCar5()
        {
            var environment = TrainTestHelper.CreateEnvironment();

            var (block, stationSaver1) = TrainTestHelper.PlaceBlockWithComponent<RailSaverComponent>(
                environment,
                ForUnitTestModBlockId.TestTrainCargoPlatform,
                new Vector3Int(-10, 1, -11),
                BlockDirection.North);
            var stationnodes1 = ExtractStationNodes(block!, stationSaver1);
            var (block2, stationSaver2) = TrainTestHelper.PlaceBlockWithComponent<RailSaverComponent>(
                environment,
                ForUnitTestModBlockId.TestTrainCargoPlatform,
                new Vector3Int(-10, 1, -11 - block.BlockPositionInfo.BlockSize.z),
                BlockDirection.North);
            var stationnodes2 = ExtractStationNodes(block2!, stationSaver2);


            var firstTrain = MasterHolder.TrainUnitMaster.Train.TrainCars.First();
            var cars = new List<TrainCar>
            {
                new TrainCar(firstTrain,true),
            };

            var railPosition = new RailPosition(new List<IRailNode> { stationnodes1.ExitFront, stationnodes1.EntryFront }, cars[0].Length, 0);
            var trainUnit = new TrainUnit(railPosition, cars, environment.GetTrainUpdateService(), environment.GetTrainRailPositionManager(), environment.GetTrainDiagramManager());

            var newCar = new TrainCar(firstTrain, true);
            var railPosition2 = new RailPosition(new List<IRailNode> { stationnodes2.ExitFront, stationnodes2.EntryFront }, newCar.Length, 0);

            trainUnit.AttachCarToRear(newCar, railPosition2);
            Assert.AreEqual(trainUnit.RailPosition.GetNodeApproaching(), stationnodes1.ExitFront, "追加approachingが不正");
            Assert.AreEqual(trainUnit.RailPosition.GetDistanceToNextNode(), 0, "追加distanceToNextNodeが不正");
            Assert.AreEqual(trainUnit.Cars.Count, 2, "追加後2両でない");
            Assert.AreEqual(trainUnit.RailPosition.TrainLength, newCar.Length * 2, "追加後編成長さが不正");
            Assert.AreEqual(trainUnit.RailPosition.GetRailNodes().Count, 4, "追加後RailNodes.Countが不正");
            foreach (var car in trainUnit.Cars)
            {
                Assert.IsTrue(car.IsFacingForward, "追加後全車前向きでない");
            }
        }

        //headにcarをつける
        //①rearの位置はnodeの真上、node距離0の状態あり
        //②追加carはnodeをまたがない
        //前後関係チェック、長さチェック、autorun耐性チェック(未)、セーブロード後のバリデーションチェック(未)
        [Test]
        public void AddCar6()
        {
            var environment = TrainTestHelper.CreateEnvironment();

            var (block, stationSaver1) = TrainTestHelper.PlaceBlockWithComponent<RailSaverComponent>(
                environment,
                ForUnitTestModBlockId.TestTrainCargoPlatform,
                new Vector3Int(-10, 1, -11),
                BlockDirection.North);
            var stationnodes1 = ExtractStationNodes(block!, stationSaver1);
            var (block2, stationSaver2) = TrainTestHelper.PlaceBlockWithComponent<RailSaverComponent>(
                environment,
                ForUnitTestModBlockId.TestTrainCargoPlatform,
                new Vector3Int(-10, 1, -11 + block.BlockPositionInfo.BlockSize.z),
                BlockDirection.North);
            var stationnodes2 = ExtractStationNodes(block2!, stationSaver2);


            var firstTrain = MasterHolder.TrainUnitMaster.Train.TrainCars.First();
            var cars = new List<TrainCar>
            {
                new TrainCar(firstTrain,true),
            };

            var railPosition = new RailPosition(new List<IRailNode> { stationnodes1.ExitFront, stationnodes1.EntryFront }, cars[0].Length, 0);
            var trainUnit = new TrainUnit(railPosition, cars, environment.GetTrainUpdateService(), environment.GetTrainRailPositionManager(), environment.GetTrainDiagramManager());

            var newCar = new TrainCar(firstTrain, true);
            var railPosition2 = new RailPosition(new List<IRailNode> { stationnodes2.ExitFront, stationnodes2.EntryFront }, newCar.Length, 0);

            trainUnit.AttachCarToHead(newCar, railPosition2);
            Assert.AreEqual(trainUnit.RailPosition.GetNodeApproaching(), stationnodes2.ExitFront, "追加approachingが不正");
            Assert.AreEqual(trainUnit.RailPosition.GetDistanceToNextNode(), 0, "追加distanceToNextNodeが不正");
            Assert.AreEqual(trainUnit.Cars.Count, 2, "追加後2両でない");
            Assert.AreEqual(trainUnit.RailPosition.TrainLength, newCar.Length * 2, "追加後編成長さが不正");
            Assert.AreEqual(trainUnit.RailPosition.GetRailNodes().Count, 4, "追加後RailNodes.Countが不正");
            foreach (var car in trainUnit.Cars)
            {
                Assert.IsTrue(car.IsFacingForward, "追加後全車前向きでない");
            }
        }
    }
}
