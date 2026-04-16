using System.Collections.Generic;
using System.Reflection;
using Core.Master;
using Core.Update;
using Game.Block.Interface;
using Game.Train.RailGraph;
using Game.Train.RailPositions;
using Game.Train.Unit;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Server.Protocol.PacketResponse;
using Tests.Module.TestMod;
using Tests.Util;
using UnityEngine;

namespace Tests.CombinedTest.Server.PacketTest
{
    public class TrainManualInputProtocolTest
    {
        [Test]
        public void TrainManualInput_ProtocolStoresLatestInputAndOperatingTrain()
        {
            // packet creator と service provider をテスト用に組み立てる
            // Build the packet creator and service provider for the test
            var (packetResponseCreator, serviceProvider) = new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var trainManualControlService = serviceProvider.GetService<TrainManualControlService>();
            var trainInstanceId = TrainInstanceId.Create();

            // protocol 経由で raw input を送り、service の保持状態を更新する
            // Send raw input through the protocol and update stored service state
            var packet = MessagePackSerializer.Serialize(
                new TrainManualInputProtocol.TrainManualInputRequestMessagePack(
                    1,
                    trainInstanceId,
                    true,
                    false,
                    true,
                    false));
            packetResponseCreator.GetPacketResponse(packet);

            // 最新入力と player -> train 対応の両方が保存されたことを確認する
            // Verify that both latest input and player -> train mapping were stored
            Assert.IsTrue(trainManualControlService.TryGetLatestInput(trainInstanceId, out var input));
            Assert.IsTrue(trainManualControlService.TryGetOperatingTrain(1, out var operatingTrainId));
            Assert.IsTrue(trainManualControlService.TryGetOperatingCar(1, out var operatingCarId));
            Assert.AreEqual(trainInstanceId, operatingTrainId);
            Assert.AreEqual(default(TrainCarInstanceId), operatingCarId);
            Assert.IsTrue(input.Forward);
            Assert.IsFalse(input.Backward);
            Assert.IsTrue(input.Left);
            Assert.IsFalse(input.Right);
        }

        [Test]
        public void TrainManualInput_NeutralInputBuildsNeutralCommand()
        {
            var environment = TrainTestHelper.CreateEnvironment();
            var train = CreateStraightTrain(environment, true);
            var trainManualControlService = ConfigureManualControl(environment, train, TrainManualRawInputState.Neutral);

            Assert.IsTrue(trainManualControlService.TryBuildManualCommand(train, out var command));
            Assert.AreEqual(0, command.MasconLevel);
            Assert.IsFalse(command.ShouldReverseUnit);
        }

        [Test]
        public void TrainManualInput_ForwardInputOnForwardFacingStoppedCarBuildsForwardCommand()
        {
            var environment = TrainTestHelper.CreateEnvironment();
            var train = CreateStraightTrain(environment, true);
            var trainManualControlService = ConfigureManualControl(
                environment,
                train,
                new TrainManualRawInputState(true, false, false, false));

            Assert.IsTrue(trainManualControlService.TryBuildManualCommand(train, out var command));
            Assert.AreEqual(MasterHolder.TrainUnitMaster.MasconLevelMaximum, command.MasconLevel);
            Assert.IsFalse(command.ShouldReverseUnit);
        }

        [Test]
        public void TrainManualInput_BackwardInputOnForwardFacingMovingCarBuildsBrakeCommand()
        {
            var environment = TrainTestHelper.CreateEnvironment();
            var train = CreateStraightTrain(environment, true);
            var trainManualControlService = ConfigureManualControl(
                environment,
                train,
                new TrainManualRawInputState(false, true, false, false));
            SetCurrentSpeed(train, 12.5);

            Assert.IsTrue(trainManualControlService.TryBuildManualCommand(train, out var command));
            Assert.AreEqual(-MasterHolder.TrainUnitMaster.MasconLevelMaximum, command.MasconLevel);
            Assert.IsFalse(command.ShouldReverseUnit);
        }

        [Test]
        public void TrainManualInput_BackwardInputOnForwardFacingStoppedCarBuildsReverseLaunchCommand()
        {
            var environment = TrainTestHelper.CreateEnvironment();
            var train = CreateStraightTrain(environment, true);
            var trainManualControlService = ConfigureManualControl(
                environment,
                train,
                new TrainManualRawInputState(false, true, false, false));

            Assert.IsTrue(trainManualControlService.TryBuildManualCommand(train, out var command));
            Assert.AreEqual(MasterHolder.TrainUnitMaster.MasconLevelMaximum, command.MasconLevel);
            Assert.IsTrue(command.ShouldReverseUnit);
        }

        [Test]
        public void TrainManualInput_ForwardInputOnBackwardFacingStoppedCarBuildsReverseLaunchCommand()
        {
            var environment = TrainTestHelper.CreateEnvironment();
            var train = CreateStraightTrain(environment, false);
            var trainManualControlService = ConfigureManualControl(
                environment,
                train,
                new TrainManualRawInputState(true, false, false, false));

            Assert.IsTrue(trainManualControlService.TryBuildManualCommand(train, out var command));
            Assert.AreEqual(MasterHolder.TrainUnitMaster.MasconLevelMaximum, command.MasconLevel);
            Assert.IsTrue(command.ShouldReverseUnit);
        }

        [Test]
        public void TrainManualInput_ConflictingInputBuildsNeutralCommand()
        {
            var environment = TrainTestHelper.CreateEnvironment();
            var train = CreateStraightTrain(environment, true);
            var trainManualControlService = ConfigureManualControl(
                environment,
                train,
                new TrainManualRawInputState(true, true, false, false));

            Assert.IsTrue(trainManualControlService.TryBuildManualCommand(train, out var command));
            Assert.AreEqual(0, command.MasconLevel);
            Assert.IsFalse(command.ShouldReverseUnit);
        }

        [Test]
        public void TrainManualInput_UpdateServiceAppliesReverseLaunchCommandBeforeUpdate()
        {
            var environment = TrainTestHelper.CreateEnvironment();
            var train = CreateStraightTrain(environment, false);
            _ = ConfigureManualControl(
                environment,
                train,
                new TrainManualRawInputState(true, false, false, false));

            // tick 前に command を組み、停止中 reverse -> 正 mascon の順で適用する
            // Resolve manual command before the tick and apply reverse before mascon
            GameUpdater.UpdateOneTick();

            Assert.IsTrue(train.Cars[0].IsFacingForward, "reverse 後に先頭車両の向きが更新されていません。");
            Assert.AreEqual(MasterHolder.TrainUnitMaster.MasconLevelMaximum, train.masconLevel);
            Assert.Greater(train.CurrentSpeed, 0, "reverse 後の正 mascon で加速していません。");
        }

        [Test]
        public void TrainManualInput_AutoRunTrainIgnoresPendingManualCommand()
        {
            var environment = TrainTestHelper.CreateEnvironment();
            var train = CreateStraightTrain(environment, true);
            train.trainDiagram.AddEntry(train.RailPosition.GetNodeApproaching());
            train.TurnOnAutoRun();
            train.SetManualCommand(new TrainManualCommand(-MasterHolder.TrainUnitMaster.MasconLevelMaximum, true));

            // auto-run 中は manual command より UpdateMasconLevel の結果を優先する
            // While auto-run is active, prefer UpdateMasconLevel over manual command
            train.Update();

            Assert.IsTrue(train.IsAutoRun, "自動運転が途中で解除されています。");
            Assert.AreEqual(MasterHolder.TrainUnitMaster.MasconLevelMaximum, train.masconLevel);
            Assert.IsTrue(train.Cars[0].IsFacingForward, "manual reverse が auto-run 中に適用されています。");
        }

        [Test]
        public void TrainManualInput_UnregisterClearsManualControlState()
        {
            var environment = TrainTestHelper.CreateEnvironment();
            var train = CreateStraightTrain(environment, true);
            var trainManualControlService = ConfigureManualControl(
                environment,
                train,
                new TrainManualRawInputState(true, false, false, false));

            // train unregister 時に raw input と player 対応をまとめて消す
            // Clear both raw input and player mappings when the train unregisters
            environment.GetITrainUnitMutationDatastore().UnregisterTrain(train);

            Assert.IsFalse(trainManualControlService.TryGetLatestInput(train.TrainInstanceId, out _));
            Assert.IsFalse(trainManualControlService.TryGetOperatingTrain(1, out _));
            Assert.IsFalse(trainManualControlService.TryGetOperatingCar(1, out _));
        }

        private static TrainManualControlService ConfigureManualControl(
            TrainTestEnvironment environment,
            TrainUnit train,
            TrainManualRawInputState input)
        {
            var trainManualControlService = environment.ServiceProvider.GetService<TrainManualControlService>();
            trainManualControlService.SetOperatingTarget(1, train.TrainInstanceId, train.Cars[0].TrainCarInstanceId);
            trainManualControlService.SetLatestInput(train.TrainInstanceId, input);
            return trainManualControlService;
        }

        private static TrainUnit CreateStraightTrain(TrainTestEnvironment environment, bool isFacingForward)
        {
            var railA = TrainTestHelper.PlaceRail(environment, new Vector3Int(0, 0, 0), BlockDirection.North);
            var railB = TrainTestHelper.PlaceRail(environment, new Vector3Int(100, 0, 0), BlockDirection.North);

            railA.FrontNode.ConnectNode(railB.FrontNode);
            railB.BackNode.ConnectNode(railA.BackNode);
            railB.FrontNode.ConnectNode(railA.FrontNode);
            railA.BackNode.ConnectNode(railB.BackNode);

            var nodeApproaching = railB.FrontNode;
            var nodeBehind = railA.FrontNode;
            var distance = nodeApproaching.GetDistanceToNode(nodeBehind);
            Assert.Greater(distance, 0, "レール間距離が正しく計算されていません。");

            var carLength = Mathf.Max(1, distance / 1024 / 20);
            var trainCar = TrainTestCarFactory.CreateTrainCarWithItemContainer(0, 240000000, 0, carLength, isFacingForward).trainCar;
            var railPosition = new RailPosition(new List<IRailNode> { nodeApproaching, nodeBehind }, trainCar.Length, Mathf.Max(1, distance / 10));
            var trainUnit = new TrainUnit(railPosition, new List<TrainCar> { trainCar }, environment.GetTrainRailPositionManager(), environment.GetTrainDiagramManager());
            environment.GetITrainUnitMutationDatastore().RegisterTrain(trainUnit);
            return trainUnit;
        }

        private static void SetCurrentSpeed(TrainUnit train, double speed)
        {
            var currentSpeedField = typeof(TrainUnit).GetField("_currentSpeed", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(currentSpeedField, "_currentSpeed の private field を取得できませんでした。");
            currentSpeedField!.SetValue(train, speed);
        }
    }
}
