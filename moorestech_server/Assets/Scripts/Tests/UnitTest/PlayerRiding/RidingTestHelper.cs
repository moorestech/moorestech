using System;
using System.Collections.Generic;
using Core.Master;
using Game.Block.Interface;
using Game.Train.RailGraph;
using Game.Train.RailPositions;
using Game.Train.Unit;
using Tests.Util;
using UnityEngine;

namespace Tests.UnitTest.PlayerRiding
{
    // 乗車システムのテスト用ヘルパ。座席付き車両を DI 上の TrainUnitDatastore へ登録する。
    // Test helper for the riding system: registers a seated car into the DI-resolved TrainUnitDatastore.
    public static class RidingTestHelper
    {
        // forUnitTest mod の train.json に定義した座席2席のテスト用 trainCar マスタの GUID。
        // GUID of the 2-seat test trainCar master defined in the forUnitTest mod's train.json.
        public static readonly Guid SeatedTrainCarGuid = Guid.Parse("22222222-2222-2222-2222-222222222222");

        // 新しいレール上に座席付き車両を1両載せた TrainUnit を作り、環境の DI 上の TrainUnitDatastore へ登録する。
        // datastore や resolver はテスト側で DI から解決すること（new 禁止）。
        // Builds a TrainUnit with one seated car and registers it into the environment's DI TrainUnitDatastore.
        public static TrainCar RegisterSeatedCarOnNewTrain(TrainTestEnvironment environment, int railZ)
        {
            var railA = TrainTestHelper.PlaceRail(environment, new Vector3Int(0, 0, railZ), BlockDirection.North);
            var railB = TrainTestHelper.PlaceRail(environment, new Vector3Int(100, 0, railZ), BlockDirection.North);
            railA.FrontNode.ConnectNode(railB.FrontNode);
            railB.BackNode.ConnectNode(railA.BackNode);
            railB.FrontNode.ConnectNode(railA.FrontNode);
            railA.BackNode.ConnectNode(railB.BackNode);

            // 座席付きテスト車両（forUnitTest mod 定義）のマスタから車両を生成する
            // Build the car from the seated test-mod master.
            var distance = railB.FrontNode.GetDistanceToNode(railA.FrontNode);
            MasterHolder.TrainUnitMaster.TryGetTrainCarMaster(SeatedTrainCarGuid, out var master);
            var car = new TrainCar(master, true);

            var railPosition = new RailPosition(
                new List<IRailNode> { railB.FrontNode, railA.FrontNode }, car.Length, Mathf.Max(1, distance / 10));
            var trainUnit = new TrainUnit(
                railPosition, new List<TrainCar> { car },
                environment.GetTrainRailPositionManager(), environment.GetTrainDiagramManager());
            environment.GetTrainUnitDatastore().RegisterTrain(trainUnit);
            return car;
        }
    }
}
