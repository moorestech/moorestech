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
    }
}
