using NUnit.Framework;
using Tests.Util;

namespace Tests.UnitTest.Game
{
    public class TrainStationDockingConcurrencyTest
    {
        [Test]
        public void ForwardTrainSuccessfullyDocks()
        {
            using var scenario = TrainStationDockingScenario.Create();

            var train = scenario.CreateForwardDockingTrain(out var car);

            Assert.IsFalse(car.IsDocked, "初期状態で車両がドッキング済みになっています。");
            Assert.IsFalse(train.trainUnitStationDocking.IsDocked, "初期状態で列車全体がドッキング済みになっています。");

            train.trainUnitStationDocking.TryDockWhenStopped();

            Assert.IsTrue(car.IsDocked, "TryDockWhenStoppedの後も車両が駅にドッキングしていません。");
            Assert.IsTrue(train.trainUnitStationDocking.IsDocked, "TryDockWhenStoppedの後も列車がドッキング状態になっていません。");
        }

        [Test]
        public void SecondForwardTrainWaitsWhileFirstIsDocked()
        {
            using var scenario = TrainStationDockingScenario.Create();

            var firstTrain = scenario.CreateForwardDockingTrain(out var firstCar);
            var secondTrain = scenario.CreateForwardDockingTrain(out var secondCar);

            firstTrain.trainUnitStationDocking.TryDockWhenStopped();
            Assert.IsTrue(firstCar.IsDocked, "先行列車が駅にドッキングできていません。");

            secondTrain.trainUnitStationDocking.TryDockWhenStopped();
            Assert.IsFalse(secondCar.IsDocked, "先行列車が占有中にもかかわらず後続列車がドッキングしています。");
            Assert.IsFalse(secondTrain.trainUnitStationDocking.IsDocked, "後続列車のドッキング状態が誤って有効になっています。");

            firstTrain.trainUnitStationDocking.UndockFromStation();

            secondTrain.trainUnitStationDocking.TryDockWhenStopped();
            Assert.IsTrue(secondCar.IsDocked, "先行列車が離脱した後も後続列車がドッキングできていません。");
            Assert.IsTrue(secondTrain.trainUnitStationDocking.IsDocked, "後続列車のドッキング状態が有効になっていません。");
        }

        [Test]
        public void OpposingTrainWaitsUntilStationIsAvailable()
        {
            using var scenario = TrainStationDockingScenario.Create();

            var frontTrain = scenario.CreateForwardDockingTrain(out var frontCar);
            var opposingTrain = scenario.CreateOpposingDockingTrain(out var opposingCar);

            frontTrain.trainUnitStationDocking.TryDockWhenStopped();
            Assert.IsTrue(frontCar.IsDocked, "正面側から到着した列車が駅にドッキングできていません。");

            opposingTrain.trainUnitStationDocking.TryDockWhenStopped();
            Assert.IsFalse(opposingCar.IsDocked, "駅が占有中にもかかわらず背面側の列車がドッキングしています。");
            Assert.IsFalse(opposingTrain.trainUnitStationDocking.IsDocked, "背面側の列車のドッキング状態が誤って有効になっています。");

            frontTrain.trainUnitStationDocking.UndockFromStation();

            opposingTrain.trainUnitStationDocking.TryDockWhenStopped();
            Assert.IsTrue(opposingCar.IsDocked, "正面側の列車が離脱した後も背面側の列車がドッキングできていません。");
            Assert.IsTrue(opposingTrain.trainUnitStationDocking.IsDocked, "背面側の列車のドッキング状態が有効になっていません。");
        }
    }
}
