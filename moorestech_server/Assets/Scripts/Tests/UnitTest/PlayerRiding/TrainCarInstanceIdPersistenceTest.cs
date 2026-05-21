using Core.Master;
using Game.Train.Unit;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;

namespace Tests.UnitTest.PlayerRiding
{
    public class TrainCarInstanceIdPersistenceTest
    {
        [Test]
        public void TrainCar_CreateSaveData_ThenRestore_KeepsSameInstanceId()
        {
            // セーブデータ作成→復元で TrainCarInstanceId が一致することを確認
            // CreateTrainCarSaveData -> RestoreTrainCar keeps the same TrainCarInstanceId.
            new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            var master = MasterHolder.TrainUnitMaster.Train.TrainCars[0];
            var car = new TrainCar(master, true);
            var originalId = car.TrainCarInstanceId;

            var saveData = car.CreateTrainCarSaveData();
            var restored = TrainCar.RestoreTrainCar(saveData);

            Assert.AreEqual(originalId, restored.TrainCarInstanceId);
        }
    }
}
