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
    public class TrainCarRidingManualCommandResolverTest
    {
        [Test]
        public void Resolve_SameTrainConflictingVotes_ReturnsNeutral()
        {
            var fixture = CreateForwardFacingFixture();
            var buffer = new TrainCarRidingInputBuffer();
            buffer.SetLatestInput(new TrainCarRidingInputBuffer.TrainCarRidingInputState(1, fixture.RidingCar.TrainCarInstanceId, 10, true, false, false, false));
            buffer.SetLatestInput(new TrainCarRidingInputBuffer.TrainCarRidingInputState(2, fixture.RidingCar.TrainCarInstanceId, 10, false, false, true, false));
            var resolver = new TrainCarRidingManualCommandResolver(fixture.TrainUnitDatastore, buffer);

            var command = resolver.Resolve(fixture.TrainUnit, 10);

            Assert.IsFalse(command.ReverseRequested, "前進票と後退票が同数なら reverse 要求は出さないべきです。");
            Assert.AreEqual(TrainUnitMasconCommand.Neutral, command.MasconCommand, "前進票と後退票が同数なら neutral になるべきです。");
        }

        [Test]
        public void Resolve_SameTrainForwardMajority_ReturnsAccelerate()
        {
            var fixture = CreateForwardFacingFixture();
            var buffer = new TrainCarRidingInputBuffer();
            buffer.SetLatestInput(new TrainCarRidingInputBuffer.TrainCarRidingInputState(1, fixture.RidingCar.TrainCarInstanceId, 10, true, false, false, false));
            buffer.SetLatestInput(new TrainCarRidingInputBuffer.TrainCarRidingInputState(2, fixture.RidingCar.TrainCarInstanceId, 10, true, false, false, false));
            buffer.SetLatestInput(new TrainCarRidingInputBuffer.TrainCarRidingInputState(3, fixture.RidingCar.TrainCarInstanceId, 10, false, false, true, false));
            var resolver = new TrainCarRidingManualCommandResolver(fixture.TrainUnitDatastore, buffer);

            var command = resolver.Resolve(fixture.TrainUnit, 10);

            Assert.IsFalse(command.ReverseRequested, "前進多数なら reverse 要求は出さないべきです。");
            Assert.AreEqual(TrainUnitMasconCommand.Accelerate, command.MasconCommand, "前進多数なら traction になるべきです。");
        }

        [Test]
        public void Resolve_SameTrainBackwardMajorityAtStop_ReturnsReverseAndTraction()
        {
            var fixture = CreateForwardFacingFixture();
            var buffer = new TrainCarRidingInputBuffer();
            buffer.SetLatestInput(new TrainCarRidingInputBuffer.TrainCarRidingInputState(1, fixture.RidingCar.TrainCarInstanceId, 10, false, false, true, false));
            buffer.SetLatestInput(new TrainCarRidingInputBuffer.TrainCarRidingInputState(2, fixture.RidingCar.TrainCarInstanceId, 10, false, false, true, false));
            buffer.SetLatestInput(new TrainCarRidingInputBuffer.TrainCarRidingInputState(3, fixture.RidingCar.TrainCarInstanceId, 10, true, false, false, false));
            var resolver = new TrainCarRidingManualCommandResolver(fixture.TrainUnitDatastore, buffer);

            var command = resolver.Resolve(fixture.TrainUnit, 10);

            Assert.IsTrue(command.ReverseRequested, "停止中の後退多数なら reverse 要求を出すべきです。");
            Assert.AreEqual(TrainUnitMasconCommand.Accelerate, command.MasconCommand, "停止中の後退多数なら reverse 後の traction になるべきです。");
        }

        [Test]
        public void Resolve_ReverseRequestWhileMoving_ReturnsBrakeUntilStop()
        {
            var fixture = CreateForwardFacingFixture();
            RunTicks(fixture.TrainUnit, 8, new TrainUnitManualCommand(false, TrainUnitMasconCommand.Accelerate));

            var buffer = new TrainCarRidingInputBuffer();
            buffer.SetLatestInput(new TrainCarRidingInputBuffer.TrainCarRidingInputState(1, fixture.RidingCar.TrainCarInstanceId, 10, false, false, true, false));
            var resolver = new TrainCarRidingManualCommandResolver(fixture.TrainUnitDatastore, buffer);

            var command = resolver.Resolve(fixture.TrainUnit, 10);
            Assert.IsFalse(command.ReverseRequested, "走行中の後退要求はまず brake になるべきです。");
            Assert.AreEqual(TrainUnitMasconCommand.Brake, command.MasconCommand, "走行中の後退要求が brake に解決されていません。");
        }

        [Test]
        public void Resolve_ReverseRequestAtStop_ReturnsReverseAndTraction()
        {
            var fixture = CreateForwardFacingFixture();

            var buffer = new TrainCarRidingInputBuffer();
            buffer.SetLatestInput(new TrainCarRidingInputBuffer.TrainCarRidingInputState(1, fixture.RidingCar.TrainCarInstanceId, 10, false, false, true, false));
            var resolver = new TrainCarRidingManualCommandResolver(fixture.TrainUnitDatastore, buffer);

            var command = resolver.Resolve(fixture.TrainUnit, 10);
            Assert.IsTrue(command.ReverseRequested, "停止中の後退要求は reverse 付きで解決されるべきです。");
            Assert.AreEqual(TrainUnitMasconCommand.Accelerate, command.MasconCommand, "停止中の後退要求は reverse 後の traction に解決されるべきです。");
        }

        [Test]
        public void Resolve_BackwardFacingCarForwardInput_ReturnsReverseAndTraction()
        {
            var fixture = CreateBackwardFacingFixture();

            var buffer = new TrainCarRidingInputBuffer();
            buffer.SetLatestInput(new TrainCarRidingInputBuffer.TrainCarRidingInputState(1, fixture.RidingCar.TrainCarInstanceId, 10, true, false, false, false));
            var resolver = new TrainCarRidingManualCommandResolver(fixture.TrainUnitDatastore, buffer);

            var command = resolver.Resolve(fixture.TrainUnit, 10);
            Assert.IsTrue(command.ReverseRequested, "後ろ向き車両での W は train 後退なので reverse 付きになるべきです。");
            Assert.AreEqual(TrainUnitMasconCommand.Accelerate, command.MasconCommand, "反転後の traction が解決されていません。");
        }

        [Test]
        public void Resolve_SameTrainBranchNextMajority_ReturnsBranchNext()
        {
            var fixture = CreateForwardFacingFixture();
            var buffer = new TrainCarRidingInputBuffer();
            buffer.SetLatestInput(new TrainCarRidingInputBuffer.TrainCarRidingInputState(1, fixture.RidingCar.TrainCarInstanceId, 10, false, false, false, true));
            buffer.SetLatestInput(new TrainCarRidingInputBuffer.TrainCarRidingInputState(2, fixture.RidingCar.TrainCarInstanceId, 10, false, false, false, true));
            buffer.SetLatestInput(new TrainCarRidingInputBuffer.TrainCarRidingInputState(3, fixture.RidingCar.TrainCarInstanceId, 10, false, true, false, false));
            var resolver = new TrainCarRidingManualCommandResolver(fixture.TrainUnitDatastore, buffer);

            var command = resolver.Resolve(fixture.TrainUnit, 10);

            Assert.AreEqual(TrainUnitMasconCommand.Neutral, command.MasconCommand, "A/D だけの入力で mascon が変化しています。");
            Assert.AreEqual(TrainUnitBranchCommand.Next, command.BranchCommand, "D 多数が next 分岐選択に解決されていません。");
        }

        [Test]
        public void Resolve_SameTrainConflictingBranchVotes_ReturnsNeutralBranch()
        {
            var fixture = CreateForwardFacingFixture();
            var buffer = new TrainCarRidingInputBuffer();
            buffer.SetLatestInput(new TrainCarRidingInputBuffer.TrainCarRidingInputState(1, fixture.RidingCar.TrainCarInstanceId, 10, false, true, false, false));
            buffer.SetLatestInput(new TrainCarRidingInputBuffer.TrainCarRidingInputState(2, fixture.RidingCar.TrainCarInstanceId, 10, false, false, false, true));
            var resolver = new TrainCarRidingManualCommandResolver(fixture.TrainUnitDatastore, buffer);

            var command = resolver.Resolve(fixture.TrainUnit, 10);

            Assert.AreEqual(TrainUnitMasconCommand.Neutral, command.MasconCommand, "A/D 同数だけの入力で mascon が変化しています。");
            Assert.AreEqual(TrainUnitBranchCommand.Neutral, command.BranchCommand, "A/D 同数が neutral 分岐選択に解決されていません。");
        }

        [Test]
        public void Resolve_NewerTickOnOtherTrain_WinsOnlyForOwningTrain()
        {
            var firstFixture = CreateForwardFacingFixture();
            var secondFixture = CreateForwardFacingFixture();

            var datastore = new TrainUnitDatastore();
            datastore.RegisterTrain(firstFixture.TrainUnit);
            datastore.RegisterTrain(secondFixture.TrainUnit);

            var buffer = new TrainCarRidingInputBuffer();
            buffer.SetLatestInput(new TrainCarRidingInputBuffer.TrainCarRidingInputState(1, firstFixture.RidingCar.TrainCarInstanceId, 10, true, false, false, false));
            buffer.SetLatestInput(new TrainCarRidingInputBuffer.TrainCarRidingInputState(2, secondFixture.RidingCar.TrainCarInstanceId, 11, false, false, true, false));
            var resolver = new TrainCarRidingManualCommandResolver(datastore, buffer);

            var firstCommand = resolver.Resolve(firstFixture.TrainUnit, 11);
            var secondCommand = resolver.Resolve(secondFixture.TrainUnit, 11);

            Assert.AreEqual(TrainUnitMasconCommand.Accelerate, firstCommand.MasconCommand, "他 train の新しい入力で対象 train の command が汚染されています。");
            Assert.AreEqual(TrainUnitMasconCommand.Accelerate, secondCommand.MasconCommand, "対象 train の最新入力が解決されていません。");
            Assert.IsTrue(secondCommand.ReverseRequested, "停止中の後退要求が reverse 付きで解決されていません。");
        }

        [Test]
        public void Resolve_InputAtTimeToLiveBoundary_ReturnsDefault()
        {
            var fixture = CreateForwardFacingFixture();
            var buffer = new TrainCarRidingInputBuffer();
            buffer.SetLatestInput(new TrainCarRidingInputBuffer.TrainCarRidingInputState(1, fixture.RidingCar.TrainCarInstanceId, 10, true, false, false, false));
            var resolver = new TrainCarRidingManualCommandResolver(fixture.TrainUnitDatastore, buffer);

            var command = resolver.Resolve(fixture.TrainUnit, 30);

            Assert.IsFalse(command.ReverseRequested, "20 tick 経過した入力は reverse 要求として扱わないべきです。");
            Assert.AreEqual(TrainUnitMasconCommand.Neutral, command.MasconCommand, "20 tick 経過した入力は neutral として扱うべきです。");
        }

        private static void RunTicks(TrainUnit trainUnit, int tickCount, TrainUnitManualCommand manualCommand)
        {
            for (var i = 0; i < tickCount; i++)
            {
                trainUnit.Update(manualCommand);
            }
        }

        private static TrainResolverFixture CreateForwardFacingFixture()
        {
            return CreateFixture(true);
        }

        private static TrainResolverFixture CreateBackwardFacingFixture()
        {
            return CreateFixture(false);
        }

        private static TrainResolverFixture CreateFixture(bool isFacingForward)
        {
            var environment = TrainTestHelper.CreateEnvironment();
            var railA = TrainTestHelper.PlaceRail(environment, new Vector3Int(0, 0, 0), BlockDirection.North);
            var railB = TrainTestHelper.PlaceRail(environment, new Vector3Int(100, 0, 0), BlockDirection.North);
            ConnectRailsBidirectional(railA, railB);

            var distance = railB.FrontNode.GetDistanceToNode(railA.FrontNode);
            var carLength = Mathf.Max(1, distance / 1024 / 20);
            var ridingCar = TrainTestCarFactory.CreateTrainCarWithItemContainer(0, 240000000, 0, carLength, isFacingForward).trainCar;
            var trainUnit = CreateTrainUnit(environment, new List<TrainCar> { ridingCar }, railA.FrontNode, railB.FrontNode, distance);

            var datastore = new TrainUnitDatastore();
            datastore.RegisterTrain(trainUnit);
            return new TrainResolverFixture(trainUnit, ridingCar, datastore);
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

        private readonly struct TrainResolverFixture
        {
            public readonly TrainUnit TrainUnit;
            public readonly TrainCar RidingCar;
            public readonly TrainUnitDatastore TrainUnitDatastore;

            public TrainResolverFixture(TrainUnit trainUnit, TrainCar ridingCar, TrainUnitDatastore trainUnitDatastore)
            {
                TrainUnit = trainUnit;
                RidingCar = ridingCar;
                TrainUnitDatastore = trainUnitDatastore;
            }
        }
    }
}
