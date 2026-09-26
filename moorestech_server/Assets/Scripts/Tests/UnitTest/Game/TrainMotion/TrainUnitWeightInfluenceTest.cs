using System;
using System.Collections.Generic;
using Core.Master;
using Core.Update;
using Game.Block.Blocks.TrainRail;
using Game.Block.Interface;
using Game.Train.RailGraph;
using Game.Train.RailPositions;
using Game.Train.Unit;
using Game.Train.Unit.Containers;
using Game.Train.Unit.Motion;
using Mooresmaster.Model.TrainModule;
using NUnit.Framework;
using Tests.Util;
using UnityEngine;

namespace Tests.UnitTest.Game.TrainMotion
{
    // 実際のTrainUnitで機関車ごとの影響度が加速に効くことを確認する
    // Verifies through a real TrainUnit that each locomotive's exponent affects acceleration
    public class TrainUnitWeightInfluenceTest
    {
        private const int LocomotiveWeight = 480000;
        private const int LocomotiveTraction = 400000;

        [Test]
        public void LowerExponentLocomotive_AcceleratesHeavyTrainFaster()
        {
            var physicsSpeed = SpeedAfterOneAccelerateTick(1f);
            var reducedInfluenceSpeed = SpeedAfterOneAccelerateTick(0.5f);

            // 影響度1は従来の物理式どおり
            // Exponent 1 matches the old physics
            var totalWeight = LocomotiveWeight + MasterHolder.TrainUnitMaster.Train.ItemContainer.Weight;
            var tractionGain = (double)LocomotiveTraction / totalWeight * GameUpdater.SecondsPerTick;
            var expectedPhysicsSpeed = tractionGain - TrainDistanceSimulator.CalculateResistanceAcceleration(tractionGain, totalWeight) * GameUpdater.SecondsPerTick;
            Assert.AreEqual(expectedPhysicsSpeed, physicsSpeed, 1e-12);

            // 基準より重いので影響度0.5が速い
            // Heavier than reference, so exponent 0.5 is faster
            Assert.Greater(reducedInfluenceSpeed, physicsSpeed * 1.5);
        }

        private static double SpeedAfterOneAccelerateTick(float locomotiveExponent)
        {
            var environment = TrainTestHelper.CreateEnvironment();
            var railA = TrainTestHelper.PlaceRail(environment, new Vector3Int(0, 0, 0), BlockDirection.North);
            var railB = TrainTestHelper.PlaceRail(environment, new Vector3Int(100, 0, 0), BlockDirection.North);
            ConnectRailsBidirectional(railA, railB);

            var distance = railB.FrontNode.GetDistanceToNode(railA.FrontNode);
            var carLength = Mathf.Max(1, distance / 1024 / 20);
            var locomotive = CreateLocomotive(locomotiveExponent, carLength);
            var railPosition = new RailPosition(new List<IRailNode> { railB.FrontNode, railA.FrontNode }, locomotive.Length, Mathf.Max(1, distance / 10));
            var trainUnit = new TrainUnit(railPosition, new List<TrainCar> { locomotive }, environment.GetTrainRailPositionManager(), environment.GetTrainDiagramManager());

            trainUnit.Update(new TrainUnitManualCommand(false, TrainUnitMasconCommand.Accelerate));
            return trainUnit.CurrentSpeed;
        }

        private static TrainCar CreateLocomotive(float exponent, int length)
        {
            var fuelItems = new[] { new TrainFuelItemsElement(0, TrainTestCarFactory.TestFuelItemGuid, (float)TrainTestCarFactory.TestFuelDuration) };
            var master = new TrainCarMasterElement(0, Guid.NewGuid(), null, false, null, LocomotiveWeight, LocomotiveTraction, exponent, 1, length, "None", 0f, fuelItems, null, 0, "TestLocomotive");
            var locomotive = new TrainCar(master, true);
            locomotive.SetContainer(ItemTrainCarContainer.CreateWithEmptySlots(1));
            locomotive.SetRemainFuelTime(TrainTestCarFactory.TestFuelDuration);
            return locomotive;
        }

        private static void ConnectRailsBidirectional(RailComponent railA, RailComponent railB)
        {
            railA.FrontNode.ConnectNode(railB.FrontNode);
            railB.BackNode.ConnectNode(railA.BackNode);
            railB.FrontNode.ConnectNode(railA.FrontNode);
            railA.BackNode.ConnectNode(railB.BackNode);
        }
    }
}
