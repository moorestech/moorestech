using System;
using Game.UnlockState;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UniRx;

namespace Tests.CombinedTest.Game
{
    public class BlockUnlockStateTest
    {
        private static readonly Guid TestBlockGuid = Guid.Parse("00000000-0000-0000-0000-000000000002");
        private static readonly Guid MachineBlockGuid = Guid.Parse("00000000-0000-0000-0000-000000000001");

        [Test]
        public void マスタのinitialUnlockedが初期状態に反映される()
        {
            var (_, serviceProvider) = CreateServer();
            var controller = serviceProvider.GetService<IGameUnlockStateDataController>();

            Assert.IsTrue(controller.BlockUnlockStateInfos[TestBlockGuid].IsUnlocked);
            Assert.IsFalse(controller.BlockUnlockStateInfos[MachineBlockGuid].IsUnlocked);
        }

        [Test]
        public void ブロック解放が保存とロードで維持される()
        {
            var (_, serviceProvider) = CreateServer();
            var controller = serviceProvider.GetService<IGameUnlockStateDataController>();

            // 解放イベントの発火も同時に検証する
            // Verify the unlock event also fires
            Guid unlockedGuid = default;
            controller.OnUnlockBlock.Subscribe(guid => unlockedGuid = guid);
            controller.UnlockBlock(MachineBlockGuid);

            Assert.AreEqual(MachineBlockGuid, unlockedGuid);
            Assert.IsTrue(controller.BlockUnlockStateInfos[MachineBlockGuid].IsUnlocked);

            // 別サーバーで状態引継ぎ確認
            // Load into another server instance and check the state carries over
            var saveJson = controller.GetSaveJsonObject();
            var (_, newServiceProvider) = CreateServer();
            var newController = newServiceProvider.GetService<IGameUnlockStateDataController>();
            newController.LoadUnlockState(saveJson);

            Assert.IsTrue(newController.BlockUnlockStateInfos[MachineBlockGuid].IsUnlocked);
            Assert.IsTrue(newController.BlockUnlockStateInfos[TestBlockGuid].IsUnlocked);
        }

        [Test]
        public void 列車車両の解放が保存とロードで維持される()
        {
            var (_, serviceProvider) = CreateServer();
            var controller = serviceProvider.GetService<IGameUnlockStateDataController>();

            // テストマスタの2両目を対象にする（1両目はinitialUnlocked:true、2両目は初期ロック）
            // Use the second train car in the test master (the first is initialUnlocked, the second stays locked)
            var trainCarGuid = global::Core.Master.MasterHolder.TrainUnitMaster.Train.TrainCars[1].TrainCarGuid;
            Assert.IsFalse(controller.TrainCarUnlockStateInfos[trainCarGuid].IsUnlocked);

            controller.UnlockTrainCar(trainCarGuid);
            var saveJson = controller.GetSaveJsonObject();

            var (_, newServiceProvider) = CreateServer();
            var newController = newServiceProvider.GetService<IGameUnlockStateDataController>();
            newController.LoadUnlockState(saveJson);

            Assert.IsTrue(newController.TrainCarUnlockStateInfos[trainCarGuid].IsUnlocked);
        }

        private static (global::Server.Protocol.PacketResponseCreator packet, ServiceProvider serviceProvider) CreateServer()
        {
            return new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
        }
    }
}
