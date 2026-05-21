using Game.PlayerRiding.Interface;
using NUnit.Framework;

namespace Tests.UnitTest.PlayerRiding
{
    public class PlayerRidingDatastoreTest
    {
        [Test]
        public void TrainCar_ImplementsIRidable_WithSeatCountFromMaster()
        {
            // TrainCar が IRidable であり、SeatCount をマスタの座席数から返すことを確認
            // TrainCar implements IRidable and exposes SeatCount from master data.
            var car = RidingTestHelper.CreateTrainCarWithSeats(2);
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
            var (resolver, _, car) = RidingTestHelper.CreateResolverWithOneTrainCar();

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
            var (datastore, car) = RidingTestHelper.CreateDatastoreWithOneTrainCar(2);
            var id = new TrainCarRidableIdentifier(car.TrainCarInstanceId.AsPrimitive());

            Assert.AreEqual(RideActionResult.Success, datastore.TryRide(1, id, out _));
            Assert.AreEqual(RideActionResult.Success, datastore.TryRide(2, id, out _));
            Assert.AreEqual(RideActionResult.NoSeatAvailable, datastore.TryRide(3, id, out _));
        }

        [Test]
        public void PlayerRidingDatastore_TryRide_RejectsWhenAlreadyRiding_AndUnknownRidable()
        {
            var (datastore, car) = RidingTestHelper.CreateDatastoreWithOneTrainCar(2);
            var id = new TrainCarRidableIdentifier(car.TrainCarInstanceId.AsPrimitive());

            Assert.AreEqual(RideActionResult.Success, datastore.TryRide(1, id, out _));
            Assert.AreEqual(RideActionResult.AlreadyRiding, datastore.TryRide(1, id, out _));
            Assert.AreEqual(RideActionResult.RidableNotFound, datastore.TryRide(2, new TrainCarRidableIdentifier(-1L), out _));
        }

        [Test]
        public void PlayerRidingDatastore_TryDismount_ClearsState()
        {
            var (datastore, car) = RidingTestHelper.CreateDatastoreWithOneTrainCar(2);
            var id = new TrainCarRidableIdentifier(car.TrainCarInstanceId.AsPrimitive());

            Assert.AreEqual(RideActionResult.NotRiding, datastore.TryDismount(1));
            datastore.TryRide(1, id, out _);
            Assert.AreEqual(RideActionResult.Success, datastore.TryDismount(1));
            Assert.IsFalse(datastore.TryGetRidingState(1, out _));
        }
    }
}
