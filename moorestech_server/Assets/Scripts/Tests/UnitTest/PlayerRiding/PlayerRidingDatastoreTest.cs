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
    }
}
