using System;
using System.Collections.Generic;
using Core.Master;
using Game.Block.Blocks.TrainRail;
using Game.Block.Interface;
using Game.PlayerConnection;
using Game.PlayerRiding;
using Game.PlayerRiding.Interface;
using Game.Train.RailGraph;
using Game.Train.RailPositions;
using Game.Train.Unit;
using Mooresmaster.Model.TrainModule;
using Tests.Util;
using UnityEngine;

namespace Tests.UnitTest.PlayerRiding
{
    // 乗車システムのテスト用ヘルパ。座席付き車両やデータストアの生成をまとめる。
    // Test helpers for the riding system: builds seated cars and datastores.
    public static class RidingTestHelper
    {
        // forUnitTest mod の train.json に定義した座席2席のテスト用 trainCar マスタの GUID。
        // GUID of the 2-seat test trainCar master defined in the forUnitTest mod's train.json.
        public static readonly Guid SeatedTrainCarGuid = Guid.Parse("22222222-2222-2222-2222-222222222222");

        // 座席付きテスト車両のマスタを取得する（マスタは JSON 定義、ここでは特定 GUID を直接参照する）。
        // Gets the seated test trainCar master by its known GUID (master is defined in JSON).
        public static TrainCarMasterElement GetSeatedTrainCarMaster()
        {
            MasterHolder.TrainUnitMaster.TryGetTrainCarMaster(SeatedTrainCarGuid, out var master);
            return master;
        }

        // TrainUnitDatastore に座席付き車両を1両登録し、RidableResolver とともに返す。
        // Registers one seated car into a TrainUnitDatastore and returns it with a RidableResolver.
        public static (RidableResolver resolver, TrainUnitDatastore datastore, TrainCar car) CreateResolverWithOneTrainCar()
        {
            var environment = TrainTestHelper.CreateEnvironment();
            var datastore = new TrainUnitDatastore();
            var car = RegisterSeatedCarOnNewTrain(environment, datastore, 0);
            return (new RidableResolver(datastore), datastore, car);
        }

        // 座席付き車両を1両登録した PlayerRidingDatastore を生成する。
        // Creates a PlayerRidingDatastore with one seated car registered.
        public static (IPlayerRidingDatastore datastore, TrainCar car) CreateDatastoreWithOneTrainCar()
        {
            var environment = TrainTestHelper.CreateEnvironment();
            var trainDatastore = new TrainUnitDatastore();
            var car = RegisterSeatedCarOnNewTrain(environment, trainDatastore, 0);
            var datastore = new PlayerRidingDatastore(new RidableResolver(trainDatastore), new AlwaysConnectedChecker());
            return (datastore, car);
        }

        // 接続状態を制御できる checker と座席付き車両1両を持つ PlayerRidingDatastore を生成する。
        // Creates a PlayerRidingDatastore with a controllable connection checker and one seated car.
        public static (IPlayerRidingDatastore datastore, TestPlayerConnectionChecker checker, TrainCar car) CreateDatastoreWithCheckerAndOneTrainCar()
        {
            var environment = TrainTestHelper.CreateEnvironment();
            var trainDatastore = new TrainUnitDatastore();
            var car = RegisterSeatedCarOnNewTrain(environment, trainDatastore, 0);
            var checker = new TestPlayerConnectionChecker();
            var datastore = new PlayerRidingDatastore(new RidableResolver(trainDatastore), checker);
            return (datastore, checker, car);
        }

        // 座席付き車両を別々の列車として2両登録した PlayerRidingDatastore を生成する。
        // Creates a PlayerRidingDatastore with two seated cars registered as separate trains.
        public static (IPlayerRidingDatastore datastore, TrainCar carA, TrainCar carB) CreateDatastoreWithTwoTrainCars()
        {
            var environment = TrainTestHelper.CreateEnvironment();
            var trainDatastore = new TrainUnitDatastore();
            var carA = RegisterSeatedCarOnNewTrain(environment, trainDatastore, 0);
            var carB = RegisterSeatedCarOnNewTrain(environment, trainDatastore, 200);
            var datastore = new PlayerRidingDatastore(new RidableResolver(trainDatastore), new AlwaysConnectedChecker());
            return (datastore, carA, carB);
        }

        // 新しいレール上に座席付き車両を1両だけ載せた TrainUnit を作り datastore に登録する。
        // Builds a TrainUnit carrying one seated car on a fresh rail pair and registers it.
        private static TrainCar RegisterSeatedCarOnNewTrain(TrainTestEnvironment environment, TrainUnitDatastore datastore, int railZ)
        {
            var railA = TrainTestHelper.PlaceRail(environment, new Vector3Int(0, 0, railZ), BlockDirection.North);
            var railB = TrainTestHelper.PlaceRail(environment, new Vector3Int(100, 0, railZ), BlockDirection.North);
            railA.FrontNode.ConnectNode(railB.FrontNode);
            railB.BackNode.ConnectNode(railA.BackNode);
            railB.FrontNode.ConnectNode(railA.FrontNode);
            railA.BackNode.ConnectNode(railB.BackNode);

            // 座席付きテスト車両（forUnitTest mod 定義）から車両を生成する
            // Build the car from the seated test-mod master.
            var distance = railB.FrontNode.GetDistanceToNode(railA.FrontNode);
            var car = new TrainCar(GetSeatedTrainCarMaster(), true);

            var railPosition = new RailPosition(
                new List<IRailNode> { railB.FrontNode, railA.FrontNode }, car.Length, Mathf.Max(1, distance / 10));
            var trainUnit = new TrainUnit(
                railPosition, new List<TrainCar> { car },
                environment.GetTrainRailPositionManager(), environment.GetTrainDiagramManager());
            datastore.RegisterTrain(trainUnit);
            return car;
        }
    }

    // テスト用の接続状態 checker。既定は全員接続中、SetDisconnected で個別に切断扱いにできる。
    // Test connection checker: everyone connected by default; SetDisconnected marks a player offline.
    public sealed class TestPlayerConnectionChecker : IPlayerConnectionChecker
    {
        private readonly HashSet<int> _disconnectedPlayerIds = new();

        public void SetDisconnected(int playerId)
        {
            _disconnectedPlayerIds.Add(playerId);
        }

        public bool IsConnected(int playerId)
        {
            return !_disconnectedPlayerIds.Contains(playerId);
        }
    }
}
