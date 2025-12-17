using System;
using System.Collections.Generic;
using Game.Block.Blocks.TrainRail;
using Game.Block.Interface;
using Game.Train.RailGraph;
using Game.Train.Train;
using Mooresmaster.Model.TrainModule;
using NUnit.Framework;
using Tests.Util;
using UnityEngine;

namespace Tests.UnitTest.Game
{
    public class TrainUnitReverseTest
    {
        [Test]
        public void Reverse_FlipsCarOrientationAndTraction()
        {
            var environment = TrainTestHelper.CreateEnvironment();
            var railA = TrainTestHelper.PlaceRail(environment, new Vector3Int(0, 0, 0), BlockDirection.North);
            var railB = TrainTestHelper.PlaceRail(environment, new Vector3Int(100, 0, 0), BlockDirection.North);

            railA.ConnectRailComponent(railB, true, true, -1);
            railB.ConnectRailComponent(railA, true, true, -1);

            var nodeApproaching = railB.FrontNode;
            var nodeBehind = railA.FrontNode;

            var distance = nodeApproaching.GetDistanceToNode(nodeBehind);
            Assert.Greater(distance, 0, "接続されたレール間の距離が正しく計算されていません。");

            var carLength = Mathf.Max(1, distance / 20);
            var frontCar = new TrainCar(new TrainCarMasterElement(Guid.Empty, Guid.Empty, null, 600000, 0, carLength), isFacingForward: true);
            var rearCar = new TrainCar(new TrainCarMasterElement(Guid.Empty, Guid.Empty, null, 300000, 0, carLength), isFacingForward: false);

            var totalLength = frontCar.Length + rearCar.Length;
            var railPosition = new RailPosition(new List<IRailNode> { nodeApproaching, nodeBehind }, totalLength, distance / 10);

            var cars = new List<TrainCar> { frontCar, rearCar };
            var trainUnit = new TrainUnit(railPosition, cars);

            double CalculateExpectedForce(IEnumerable<TrainCar> targetCars)
            {
                int totalWeight = 0;
                int totalTraction = 0;
                foreach (var car in targetCars)
                {
                    var (weight, traction) = car.GetWeightAndTraction();
                    totalWeight += weight;
                    totalTraction += traction;
                }

                return totalTraction == 0 ? 0 : (double)totalTraction / totalWeight;
            }

            var expectedForwardForce = CalculateExpectedForce(trainUnit.Cars);
            var actualForwardForce = trainUnit.UpdateTractionForce(16777216);
            Assert.AreEqual(expectedForwardForce, actualForwardForce, 1e-6, "前進時の牽引力計算が想定と一致しません。");

            trainUnit.Reverse();

            Assert.AreSame(rearCar, trainUnit.Cars[0], "編成の先頭車両が反転後に更新されていません。");
            Assert.IsTrue(rearCar.IsFacingForward, "反転後に先頭車両が前向きに更新されていません。");
            Assert.IsFalse(frontCar.IsFacingForward, "反転後に元の先頭車両が後ろ向きになっていません。");

            var expectedReverseForce = CalculateExpectedForce(trainUnit.Cars);
            var actualReverseForce = trainUnit.UpdateTractionForce(16777216);
            Assert.AreEqual(expectedReverseForce, actualReverseForce, 1e-6, "反転後の牽引力計算が想定と一致しません。");
        }
    }
}
