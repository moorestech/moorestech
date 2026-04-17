using System.Collections.Generic;
using Game.Block.Blocks.TrainRail;
using Game.Block.Interface;
using Game.Train.RailGraph;
using Game.Train.RailPositions;
using Game.Train.Unit;
using NUnit.Framework;
using Tests.Util;
using UnityEngine;

namespace Tests.UnitTest.Game
{
    public class TrainUnitManualCommandTest
    {
        private const int TractionMasconLevel = 16777216;
        private const int NeutralMasconLevel = 0;
        private const int BrakeMasconLevel = -16777216;

        [Test]
        public void ManualTraction_AcceleratesForward()
        {
            var fixture = CreateSingleCarFixture();

            fixture.TrainUnit.SetManualCommand(new TrainUnitManualCommand(false, 1));
            RunTicks(fixture.TrainUnit, 8);

            Assert.AreEqual(TractionMasconLevel, fixture.TrainUnit.masconLevel, "manual traction が masconLevel=+16777216 に変換されていません。");
            Assert.Greater(fixture.TrainUnit.CurrentSpeed, 0d, "manual traction で前進加速を開始できていません。");
        }

        [Test]
        public void ManualBrake_ReducesSpeed()
        {
            var fixture = CreateSingleCarFixture();

            fixture.TrainUnit.SetManualCommand(new TrainUnitManualCommand(false, 1));
            RunTicks(fixture.TrainUnit, 8);
            var speedBeforeBrake = fixture.TrainUnit.CurrentSpeed;

            fixture.TrainUnit.SetManualCommand(new TrainUnitManualCommand(false, -1));
            RunTicks(fixture.TrainUnit, 4);

            Assert.AreEqual(BrakeMasconLevel, fixture.TrainUnit.masconLevel, "manual brake が masconLevel=-16777216 に変換されていません。");
            Assert.Less(fixture.TrainUnit.CurrentSpeed, speedBeforeBrake, "manual brake で速度が低下していません。");
        }

        [Test]
        public void ManualReverseWithTraction_ReversesAndAcceleratesOnSameTick()
        {
            var fixture = CreateTwoCarFixture();

            fixture.TrainUnit.SetManualCommand(new TrainUnitManualCommand(true, 1));
            fixture.TrainUnit.Update();

            Assert.AreSame(fixture.RearCar, fixture.TrainUnit.Cars[0], "停止中 reverse 後に編成順序が反転していません。");
            Assert.IsTrue(fixture.RearCar.IsFacingForward, "停止中 reverse 後に新しい先頭車両の向きが前向きになっていません。");
            Assert.IsFalse(fixture.FrontCar.IsFacingForward, "停止中 reverse 後に元の先頭車両の向きが反転していません。");
            Assert.AreEqual(TractionMasconLevel, fixture.TrainUnit.masconLevel, "reverse 後の traction が masconLevel=+16777216 に変換されていません。");
            Assert.Greater(fixture.TrainUnit.CurrentSpeed, 0d, "reverse した tick で traction による加速が始まっていません。");
        }

        [Test]
        public void ManualReverseWhileMoving_IsIgnored()
        {
            var fixture = CreateTwoCarFixture();

            fixture.TrainUnit.SetManualCommand(new TrainUnitManualCommand(false, 1));
            RunTicks(fixture.TrainUnit, 8);
            Assert.Greater(fixture.TrainUnit.CurrentSpeed, 0d, "reverse 無視テストの前提となる前進加速ができていません。");

            fixture.TrainUnit.SetManualCommand(new TrainUnitManualCommand(true, 1));
            fixture.TrainUnit.Update();

            Assert.AreSame(fixture.FrontCar, fixture.TrainUnit.Cars[0], "走行中 reverse request が無視されず編成順序が反転しています。");
            Assert.IsTrue(fixture.FrontCar.IsFacingForward, "走行中 reverse request が無視されず車両向きが変わっています。");
        }

        [Test]
        public void ManualReverseWithNeutral_ReversesWithoutAcceleration()
        {
            var fixture = CreateTwoCarFixture();

            fixture.TrainUnit.SetManualCommand(new TrainUnitManualCommand(true, 0));
            fixture.TrainUnit.Update();

            Assert.AreSame(fixture.RearCar, fixture.TrainUnit.Cars[0], "neutral 付き reverse で編成順序が反転していません。");
            Assert.AreEqual(NeutralMasconLevel, fixture.TrainUnit.masconLevel, "manual neutral が masconLevel=0 に変換されていません。");
            Assert.AreEqual(0d, fixture.TrainUnit.CurrentSpeed, "neutral 付き reverse で加速してしまっています。");
        }

        [Test]
        public void ManualCommand_IsIgnoredDuringAutoRun()
        {
            var fixture = CreateTwoCarFixture();

            fixture.TrainUnit.trainDiagram.AddEntry(fixture.DestinationNode);
            fixture.TrainUnit.TurnOnAutoRun();
            fixture.TrainUnit.SetManualCommand(new TrainUnitManualCommand(true, -1));
            fixture.TrainUnit.Update();

            Assert.IsTrue(fixture.TrainUnit.IsAutoRun, "auto-run が意図せず解除されています。");
            Assert.AreSame(fixture.FrontCar, fixture.TrainUnit.Cars[0], "auto-run 中に manual reverse が適用されています。");
            Assert.AreEqual(TractionMasconLevel, fixture.TrainUnit.masconLevel, "auto-run 中に manual brake が auto-run mascon を上書きしています。");
            Assert.Greater(fixture.TrainUnit.CurrentSpeed, 0d, "auto-run 中に manual traction 無視の検証に必要な加速が発生していません。");
        }

        [Test]
        public void ManualReverseWithBrake_AtStopReversesAndAppliesBrakeMascon()
        {
            var fixture = CreateTwoCarFixture();

            fixture.TrainUnit.SetManualCommand(new TrainUnitManualCommand(true, -1));
            fixture.TrainUnit.Update();

            Assert.AreSame(fixture.RearCar, fixture.TrainUnit.Cars[0], "停止中 reverse+brake で reverse が実行されていません。");
            Assert.AreEqual(BrakeMasconLevel, fixture.TrainUnit.masconLevel, "reverse+brake が masconLevel=-16777216 を適用していません。");
            Assert.AreEqual(0d, fixture.TrainUnit.CurrentSpeed, "停止中 reverse+brake で速度が 0 のままになっていません。");
        }

        [Test]
        public void ManualReverseRequest_ReversesEveryTickWhenReissuedAtStop()
        {
            var fixture = CreateTwoCarFixture();

            fixture.TrainUnit.SetManualCommand(new TrainUnitManualCommand(true, 0));
            fixture.TrainUnit.Update();
            Assert.AreSame(fixture.RearCar, fixture.TrainUnit.Cars[0], "1 回目の reverse request で編成が反転していません。");

            fixture.TrainUnit.SetManualCommand(new TrainUnitManualCommand(true, 0));
            fixture.TrainUnit.Update();
            Assert.AreSame(fixture.FrontCar, fixture.TrainUnit.Cars[0], "停止中に再送した reverse request が 2 回目に適用されていません。");
            Assert.AreEqual(0d, fixture.TrainUnit.CurrentSpeed, "停止中の連続 reverse request で速度が変化しています。");
        }

        [Test]
        public void ManualReverse_PreservesFormationAndTractionCalculation()
        {
            var fixture = CreateTwoCarFixture();

            fixture.TrainUnit.SetManualCommand(new TrainUnitManualCommand(true, 0));
            fixture.TrainUnit.Update();

            Assert.AreSame(fixture.RearCar, fixture.TrainUnit.Cars[0], "manual reverse 後に先頭車両が更新されていません。");
            Assert.IsTrue(fixture.RearCar.IsFacingForward, "manual reverse 後に新しい先頭車両の向きが前向きになっていません。");
            Assert.IsFalse(fixture.FrontCar.IsFacingForward, "manual reverse 後に元の先頭車両の向きが反転していません。");

            var expectedTractionForce = CalculateExpectedForce(fixture.TrainUnit.Cars);
            var actualTractionForce = fixture.TrainUnit.UpdateTractionForce(TractionMasconLevel);

            Assert.AreEqual(expectedTractionForce, actualTractionForce, 1e-6, "manual reverse 後に牽引力計算が壊れています。");
        }

        private static void RunTicks(TrainUnit trainUnit, int tickCount)
        {
            for (var i = 0; i < tickCount; i++)
            {
                trainUnit.Update();
            }
        }

        private static double CalculateExpectedForce(IReadOnlyList<TrainCar> cars)
        {
            var totalWeight = 0;
            var totalTraction = 0;

            foreach (var car in cars)
            {
                var (weight, traction) = car.GetWeightAndTraction(TractionMasconLevel);
                totalWeight += weight;
                totalTraction += traction;
            }

            if (totalTraction == 0)
            {
                return 0;
            }

            return (double)totalTraction / totalWeight;
        }

        private static TrainFixture CreateSingleCarFixture()
        {
            var environment = TrainTestHelper.CreateEnvironment();
            var railA = TrainTestHelper.PlaceRail(environment, new Vector3Int(0, 0, 0), BlockDirection.North);
            var railB = TrainTestHelper.PlaceRail(environment, new Vector3Int(100, 0, 0), BlockDirection.North);
            ConnectRailsBidirectional(railA, railB);

            var distance = railB.FrontNode.GetDistanceToNode(railA.FrontNode);
            Assert.Greater(distance, 0, "テスト用レール間の距離が正しく計算できていません。");

            var carLength = Mathf.Max(1, distance / 1024 / 20);
            var frontCar = TrainTestCarFactory.CreateTrainCarWithItemContainer(0, 240000000, 0, carLength, true).trainCar;
            var trainUnit = CreateTrainUnit(environment, new List<TrainCar> { frontCar }, railA.FrontNode, railB.FrontNode, distance);

            return new TrainFixture(trainUnit, frontCar, null, railA.FrontNode);
        }

        private static TrainFixture CreateTwoCarFixture()
        {
            var environment = TrainTestHelper.CreateEnvironment();
            var railA = TrainTestHelper.PlaceRail(environment, new Vector3Int(0, 0, 0), BlockDirection.North);
            var railB = TrainTestHelper.PlaceRail(environment, new Vector3Int(100, 0, 0), BlockDirection.North);
            ConnectRailsBidirectional(railA, railB);

            var distance = railB.FrontNode.GetDistanceToNode(railA.FrontNode);
            Assert.Greater(distance, 0, "テスト用レール間の距離が正しく計算できていません。");

            var carLength = Mathf.Max(1, distance / 1024 / 20);
            var frontCar = TrainTestCarFactory.CreateTrainCarWithItemContainer(0, 240000000, 0, carLength, true).trainCar;
            var rearCar = TrainTestCarFactory.CreateTrainCarWithItemContainer(1, 120000000, 0, carLength, false).trainCar;
            var trainUnit = CreateTrainUnit(environment, new List<TrainCar> { frontCar, rearCar }, railA.FrontNode, railB.FrontNode, distance);

            return new TrainFixture(trainUnit, frontCar, rearCar, railA.FrontNode);
        }

        private static TrainUnit CreateTrainUnit(TrainTestEnvironment environment, List<TrainCar> cars, IRailNode nodeBehind, IRailNode nodeApproaching, int nodeDistance)
        {
            var totalLength = 0;
            foreach (var car in cars)
            {
                totalLength += car.Length;
            }

            var railPosition = new RailPosition(new List<IRailNode> { nodeApproaching, nodeBehind }, totalLength, Mathf.Max(1, nodeDistance / 10));
            return new TrainUnit(railPosition, cars, environment.GetTrainRailPositionManager(), environment.GetTrainDiagramManager());
        }

        private static void ConnectRailsBidirectional(RailComponent railA, RailComponent railB)
        {
            railA.FrontNode.ConnectNode(railB.FrontNode);
            railB.BackNode.ConnectNode(railA.BackNode);
            railB.FrontNode.ConnectNode(railA.FrontNode);
            railA.BackNode.ConnectNode(railB.BackNode);
        }

        private readonly struct TrainFixture
        {
            public readonly TrainUnit TrainUnit;
            public readonly TrainCar FrontCar;
            public readonly TrainCar RearCar;
            public readonly IRailNode DestinationNode;

            public TrainFixture(TrainUnit trainUnit, TrainCar frontCar, TrainCar rearCar, IRailNode destinationNode)
            {
                TrainUnit = trainUnit;
                FrontCar = frontCar;
                RearCar = rearCar;
                DestinationNode = destinationNode;
            }
        }
    }
}
