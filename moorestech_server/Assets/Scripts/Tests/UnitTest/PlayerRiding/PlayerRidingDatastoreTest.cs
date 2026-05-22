using System.Collections.Generic;
using Game.PlayerConnection;
using Game.PlayerRiding;
using Game.PlayerRiding.Interface;
using Game.Train.Unit;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Tests.Util;

namespace Tests.UnitTest.PlayerRiding
{
    public class PlayerRidingDatastoreTest
    {
        [Test]
        public void TrainCar_ImplementsIRidable_WithSeatCountFromMaster()
        {
            // TrainCar が IRidable であり、SeatCount をマスタの座席数から返すことを確認
            // TrainCar implements IRidable and exposes SeatCount from master data.
            var environment = TrainTestHelper.CreateEnvironment();
            var car = RidingTestHelper.RegisterSeatedCarOnNewTrain(environment, 0);
            IRidable ridable = car;

            Assert.IsInstanceOf<TrainCarRidableIdentifier>(ridable.Identifier);
            Assert.AreEqual(2, ridable.SeatCount);
            Assert.AreEqual(car.TrainCarInstanceId.AsPrimitive(),
                ((TrainCarRidableIdentifier)ridable.Identifier).TrainCarInstanceId);
        }

        [Test]
        public void RidableResolver_ResolvesExistingTrainCar_AndReturnsNullForMissing()
        {
            // 登録済み車両は解決でき、未知のIDは null を返す
            // A registered car resolves; an unknown id returns null.
            var environment = TrainTestHelper.CreateEnvironment();
            var car = RidingTestHelper.RegisterSeatedCarOnNewTrain(environment, 0);
            var resolver = environment.ServiceProvider.GetService<RidableResolver>();

            var existing = resolver.Resolve(new TrainCarRidableIdentifier(car.TrainCarInstanceId.AsPrimitive()));
            var missing = resolver.Resolve(new TrainCarRidableIdentifier(-1L));

            Assert.IsNotNull(existing);
            Assert.AreEqual(car.TrainCarInstanceId.AsPrimitive(),
                ((TrainCarRidableIdentifier)existing.Identifier).TrainCarInstanceId);
            Assert.IsNull(missing);
        }

        [Test]
        public void PlayerRidingDatastore_TryRide_AssignsFreeSeat_AndRejectsWhenFull()
        {
            // 座席2席の車両: 1人目・2人目は乗車成功、3人目は NoSeatAvailable
            // A 2-seat car: first and second riders succeed, third gets NoSeatAvailable.
            var environment = TrainTestHelper.CreateEnvironment();
            var car = RidingTestHelper.RegisterSeatedCarOnNewTrain(environment, 0);
            var datastore = environment.ServiceProvider.GetService<IPlayerRidingDatastore>();
            RegisterConnectedPlayers(environment, 1, 2, 3);
            var id = new TrainCarRidableIdentifier(car.TrainCarInstanceId.AsPrimitive());

            Assert.AreEqual(RideActionResult.Success, datastore.TryRide(1, id, out _));
            Assert.AreEqual(RideActionResult.Success, datastore.TryRide(2, id, out _));
            Assert.AreEqual(RideActionResult.NoSeatAvailable, datastore.TryRide(3, id, out _));
        }

        [Test]
        public void PlayerRidingDatastore_TryRide_RejectsWhenAlreadyRiding_AndUnknownRidable()
        {
            var environment = TrainTestHelper.CreateEnvironment();
            var car = RidingTestHelper.RegisterSeatedCarOnNewTrain(environment, 0);
            var datastore = environment.ServiceProvider.GetService<IPlayerRidingDatastore>();
            RegisterConnectedPlayers(environment, 1, 2);
            var id = new TrainCarRidableIdentifier(car.TrainCarInstanceId.AsPrimitive());

            Assert.AreEqual(RideActionResult.Success, datastore.TryRide(1, id, out _));
            Assert.AreEqual(RideActionResult.AlreadyRiding, datastore.TryRide(1, id, out _));
            Assert.AreEqual(RideActionResult.RidableNotFound, datastore.TryRide(2, new TrainCarRidableIdentifier(-1L), out _));
        }

        [Test]
        public void PlayerRidingDatastore_TryDismount_ClearsState()
        {
            var environment = TrainTestHelper.CreateEnvironment();
            var car = RidingTestHelper.RegisterSeatedCarOnNewTrain(environment, 0);
            var datastore = environment.ServiceProvider.GetService<IPlayerRidingDatastore>();
            RegisterConnectedPlayers(environment, 1);
            var id = new TrainCarRidableIdentifier(car.TrainCarInstanceId.AsPrimitive());

            Assert.AreEqual(RideActionResult.NotRiding, datastore.TryDismount(1));
            datastore.TryRide(1, id, out _);
            Assert.AreEqual(RideActionResult.Success, datastore.TryDismount(1));
            Assert.IsFalse(datastore.TryGetRidingState(1, out _));
        }

        [Test]
        public void PlayerRidingDatastore_OnRidableRemoved_DismountsRidersOfThatRidable()
        {
            // 乗り物Aに乗っているプレイヤーは OnRidableRemoved(A) でクリアされ、他乗り物の乗員は残る
            // Riders of removed ridable A are cleared; riders of other ridables remain.
            var environment = TrainTestHelper.CreateEnvironment();
            var carA = RidingTestHelper.RegisterSeatedCarOnNewTrain(environment, 0);
            var carB = RidingTestHelper.RegisterSeatedCarOnNewTrain(environment, 200);
            var datastore = environment.ServiceProvider.GetService<IPlayerRidingDatastore>();
            RegisterConnectedPlayers(environment, 1, 2);
            var idA = new TrainCarRidableIdentifier(carA.TrainCarInstanceId.AsPrimitive());
            var idB = new TrainCarRidableIdentifier(carB.TrainCarInstanceId.AsPrimitive());
            datastore.TryRide(1, idA, out _);
            datastore.TryRide(2, idB, out _);

            var dismounted = datastore.OnRidableRemoved(idA);

            CollectionAssert.Contains(dismounted, 1);
            Assert.AreEqual(1, dismounted.Count);
            Assert.IsFalse(datastore.TryGetRidingState(1, out _));
            Assert.IsTrue(datastore.TryGetRidingState(2, out _));
        }

        [Test]
        public void PlayerRidingDatastore_OnRidableRemoved_IsIdempotent()
        {
            // 既に降車済みに対する OnRidableRemoved は no-op（冪等。仕様書セクション4.4）
            // OnRidableRemoved on an already-cleared ridable is a no-op (idempotent).
            var environment = TrainTestHelper.CreateEnvironment();
            var carA = RidingTestHelper.RegisterSeatedCarOnNewTrain(environment, 0);
            var datastore = environment.ServiceProvider.GetService<IPlayerRidingDatastore>();
            RegisterConnectedPlayers(environment, 1);
            var idA = new TrainCarRidableIdentifier(carA.TrainCarInstanceId.AsPrimitive());
            datastore.TryRide(1, idA, out _);

            datastore.OnRidableRemoved(idA);
            var second = datastore.OnRidableRemoved(idA);

            Assert.AreEqual(0, second.Count);
        }

        [Test]
        public void PlayerRidingDatastore_OnRidableRemoved_KeepsDisconnectedRiders()
        {
            // 接続中の乗員のみ降車させ、切断中の乗員は RidingState を残す（仕様書セクション4.4）
            // Only connected riders are dismounted; disconnected riders keep their RidingState.
            var environment = TrainTestHelper.CreateEnvironment();
            var car = RidingTestHelper.RegisterSeatedCarOnNewTrain(environment, 0);
            var datastore = environment.ServiceProvider.GetService<IPlayerRidingDatastore>();
            var registry = RegisterConnectedPlayers(environment, 1, 2);
            var id = new TrainCarRidableIdentifier(car.TrainCarInstanceId.AsPrimitive());
            datastore.TryRide(1, id, out _);
            datastore.TryRide(2, id, out _);
            registry.Unregister(2);

            var dismounted = datastore.OnRidableRemoved(id);

            CollectionAssert.Contains(dismounted, 1);
            Assert.AreEqual(1, dismounted.Count);
            Assert.IsFalse(datastore.TryGetRidingState(1, out _));
            Assert.IsTrue(datastore.TryGetRidingState(2, out _));
        }

        [Test]
        public void PlayerRidingDatastore_EvaluateOnLogin_RestoresWhenSeatValidAndFree()
        {
            // 乗り物が存在し記録席が有効・空き → 復帰（RidingState 維持）
            // Ridable exists, recorded seat valid and free -> restored (RidingState kept).
            var environment = TrainTestHelper.CreateEnvironment();
            var car = RidingTestHelper.RegisterSeatedCarOnNewTrain(environment, 0);
            var datastore = environment.ServiceProvider.GetService<IPlayerRidingDatastore>();
            datastore.LoadSaveData(new List<PlayerRidingSaveData>
            {
                new() { PlayerId = 1, RidableType = RidableType.TrainCar.AsPrimitive(), IdentifierState = car.TrainCarInstanceId.AsPrimitive().ToString(), SeatIndex = 0 },
            });

            var restored = datastore.EvaluateOnLogin(1);

            Assert.IsTrue(restored);
            Assert.IsTrue(datastore.TryGetRidingState(1, out _));
        }

        [Test]
        public void PlayerRidingDatastore_EvaluateOnLogin_DismountsWhenRidableMissingOrSeatOutOfRange()
        {
            // 乗り物消失・席範囲外はいずれも復帰不可でクリアされる（仕様書セクション8）
            // Missing ridable or out-of-range seat both fail to restore and are cleared.
            var environment = TrainTestHelper.CreateEnvironment();
            var car = RidingTestHelper.RegisterSeatedCarOnNewTrain(environment, 0);
            var datastore = environment.ServiceProvider.GetService<IPlayerRidingDatastore>();
            datastore.LoadSaveData(new List<PlayerRidingSaveData>
            {
                new() { PlayerId = 1, RidableType = RidableType.TrainCar.AsPrimitive(), IdentifierState = "-1", SeatIndex = 0 },
                new() { PlayerId = 2, RidableType = RidableType.TrainCar.AsPrimitive(), IdentifierState = car.TrainCarInstanceId.AsPrimitive().ToString(), SeatIndex = 99 },
            });

            Assert.IsFalse(datastore.EvaluateOnLogin(1));
            Assert.IsFalse(datastore.EvaluateOnLogin(2));
            Assert.IsFalse(datastore.TryGetRidingState(1, out _));
            Assert.IsFalse(datastore.TryGetRidingState(2, out _));
        }

        [Test]
        public void PlayerRidingDatastore_SaveData_RoundTrips()
        {
            // GetSaveData → LoadSaveData で乗車状態が往復することを確認
            // Riding state round-trips through GetSaveData -> LoadSaveData.
            var environment = TrainTestHelper.CreateEnvironment();
            var car = RidingTestHelper.RegisterSeatedCarOnNewTrain(environment, 0);
            var datastore = environment.ServiceProvider.GetService<IPlayerRidingDatastore>();
            RegisterConnectedPlayers(environment, 7);
            var id = new TrainCarRidableIdentifier(car.TrainCarInstanceId.AsPrimitive());
            datastore.TryRide(7, id, out _);

            var saveData = datastore.GetSaveData();

            var environment2 = TrainTestHelper.CreateEnvironment();
            var datastore2 = environment2.ServiceProvider.GetService<IPlayerRidingDatastore>();
            datastore2.LoadSaveData(saveData);

            Assert.IsTrue(datastore2.TryGetRidingState(7, out var state));
            Assert.AreEqual(0, state.SeatIndex);
            Assert.IsTrue(state.Identifier.Equals(id));
        }

        private static PlayerConnectionRegistry RegisterConnectedPlayers(TrainTestEnvironment environment, params int[] playerIds)
        {
            var registry = (PlayerConnectionRegistry)environment.ServiceProvider.GetService<IPlayerConnectionChecker>();

            // 乗車判定は実機側と同じ接続レジストリを IPlayerConnectionChecker として使う。
            // Riding checks use the same connection registry as the runtime IPlayerConnectionChecker.
            foreach (var playerId in playerIds)
            {
                registry.Register(playerId);
            }

            return registry;
        }
    }
}
