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
        // 指定座席数のマスタを持つ単体 TrainCar を生成する。
        // Creates a standalone TrainCar whose master has the given seat count.
        public static TrainCar CreateTrainCarWithSeats(int seatCount)
        {
            // TrainCar コンストラクタが ServerContext を参照するため DI 環境を先に用意する
            // The TrainCar constructor reads ServerContext, so set up the DI environment first.
            TrainTestHelper.CreateEnvironment();
            return new TrainCar(FindTrainCarMasterWithSeatCount(seatCount), true);
        }

        // テストMOD（forUnitTest）の train.json から、指定座席数の trainCar マスタを取得する。
        // マスタは JSON で定義する方針のため、テストコード内でマスタを動的生成しない。
        // Looks up the trainCar master with the given seat count from the test mod's train.json.
        public static TrainCarMasterElement FindTrainCarMasterWithSeatCount(int seatCount)
        {
            foreach (var master in MasterHolder.TrainUnitMaster.Train.TrainCars)
            {
                if (master.RidableSeats.Length == seatCount)
                {
                    return master;
                }
            }
            throw new InvalidOperationException($"forUnitTest mod に座席数 {seatCount} の trainCar マスタが定義されていません。");
        }

        // TrainUnitDatastore に座席付き車両を1両登録し、RidableResolver とともに返す。
        // Registers one seated car into a TrainUnitDatastore and returns it with a RidableResolver.
        public static (RidableResolver resolver, TrainUnitDatastore datastore, TrainCar car) CreateResolverWithOneTrainCar()
        {
            var environment = TrainTestHelper.CreateEnvironment();
            var datastore = new TrainUnitDatastore();
            var car = RegisterSeatedCarOnNewTrain(environment, datastore, 1, 0);
            return (new RidableResolver(datastore), datastore, car);
        }

        // 座席付き車両を1両登録した PlayerRidingDatastore を生成する。
        // Creates a PlayerRidingDatastore with one seated car registered.
        public static (PlayerRidingDatastore datastore, TrainCar car) CreateDatastoreWithOneTrainCar(int seatCount)
        {
            var environment = TrainTestHelper.CreateEnvironment();
            var trainDatastore = new TrainUnitDatastore();
            var car = RegisterSeatedCarOnNewTrain(environment, trainDatastore, seatCount, 0);
            var datastore = new PlayerRidingDatastore(new RidableResolver(trainDatastore), new AlwaysConnectedChecker());
            return (datastore, car);
        }

        // 接続状態を制御できる checker と座席付き車両1両を持つ PlayerRidingDatastore を生成する。
        // Creates a PlayerRidingDatastore with a controllable connection checker and one seated car.
        public static (PlayerRidingDatastore datastore, TestPlayerConnectionChecker checker, TrainCar car) CreateDatastoreWithCheckerAndOneTrainCar(int seatCount)
        {
            var environment = TrainTestHelper.CreateEnvironment();
            var trainDatastore = new TrainUnitDatastore();
            var car = RegisterSeatedCarOnNewTrain(environment, trainDatastore, seatCount, 0);
            var checker = new TestPlayerConnectionChecker();
            var datastore = new PlayerRidingDatastore(new RidableResolver(trainDatastore), checker);
            return (datastore, checker, car);
        }

        // 座席付き車両を別々の列車として2両登録した PlayerRidingDatastore を生成する。
        // Creates a PlayerRidingDatastore with two seated cars registered as separate trains.
        public static (PlayerRidingDatastore datastore, TrainCar carA, TrainCar carB) CreateDatastoreWithTwoTrainCars(int seatCount)
        {
            var environment = TrainTestHelper.CreateEnvironment();
            var trainDatastore = new TrainUnitDatastore();
            var carA = RegisterSeatedCarOnNewTrain(environment, trainDatastore, seatCount, 0);
            var carB = RegisterSeatedCarOnNewTrain(environment, trainDatastore, seatCount, 200);
            var datastore = new PlayerRidingDatastore(new RidableResolver(trainDatastore), new AlwaysConnectedChecker());
            return (datastore, carA, carB);
        }

        // 新しいレール上に座席付き車両を1両だけ載せた TrainUnit を作り datastore に登録する。
        // Builds a TrainUnit carrying one seated car on a fresh rail pair and registers it.
        private static TrainCar RegisterSeatedCarOnNewTrain(TrainTestEnvironment environment, TrainUnitDatastore datastore, int seatCount, int railZ)
        {
            var railA = TrainTestHelper.PlaceRail(environment, new Vector3Int(0, 0, railZ), BlockDirection.North);
            var railB = TrainTestHelper.PlaceRail(environment, new Vector3Int(100, 0, railZ), BlockDirection.North);
            railA.FrontNode.ConnectNode(railB.FrontNode);
            railB.BackNode.ConnectNode(railA.BackNode);
            railB.FrontNode.ConnectNode(railA.FrontNode);
            railA.BackNode.ConnectNode(railB.BackNode);

            // 座席数に対応するマスタ（forUnitTest mod 定義）から車両を生成する
            // Build the car from the test-mod master that has the requested seat count.
            var distance = railB.FrontNode.GetDistanceToNode(railA.FrontNode);
            var car = new TrainCar(FindTrainCarMasterWithSeatCount(seatCount), true);

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
