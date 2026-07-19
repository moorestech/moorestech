using System;
using Game.UnlockState;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UniRx;

namespace Tests.CombinedTest.Game
{
    public class ConnectToolUnlockStateTest
    {
        // テストマスタのconnectTools（3件とも initialUnlocked:false）
        // Connect tools in the test master (all three have initialUnlocked:false)
        private static readonly Guid ElectricWireGuid = Guid.Parse("c0000000-0000-0000-0000-000000000001");
        private static readonly Guid RailGuid = Guid.Parse("c0000000-0000-0000-0000-000000000002");

        [Test]
        public void マスタのinitialUnlockedが初期状態に反映される()
        {
            var (_, serviceProvider) = CreateServer();
            var controller = serviceProvider.GetService<IGameUnlockStateDataController>();

            Assert.IsFalse(controller.ConnectToolUnlockStateInfos[ElectricWireGuid].IsUnlocked);
            Assert.IsFalse(controller.ConnectToolUnlockStateInfos[RailGuid].IsUnlocked);
        }

        [Test]
        public void 接続ツールの解放が保存とロードで維持される()
        {
            var (_, serviceProvider) = CreateServer();
            var controller = serviceProvider.GetService<IGameUnlockStateDataController>();

            // 解放イベントの発火も同時に検証する
            // Verify the unlock event also fires
            Guid unlockedGuid = default;
            controller.OnUnlockConnectTool.Subscribe(guid => unlockedGuid = guid);
            controller.UnlockConnectTool(ElectricWireGuid);

            Assert.AreEqual(ElectricWireGuid, unlockedGuid);
            Assert.IsTrue(controller.ConnectToolUnlockStateInfos[ElectricWireGuid].IsUnlocked);

            // 別サーバーで状態引継ぎ確認
            // Load into another server instance and check the state carries over
            var saveJson = controller.GetSaveJsonObject();
            var (_, newServiceProvider) = CreateServer();
            var newController = newServiceProvider.GetService<IGameUnlockStateDataController>();
            newController.LoadUnlockState(saveJson);

            Assert.IsTrue(newController.ConnectToolUnlockStateInfos[ElectricWireGuid].IsUnlocked);
            Assert.IsFalse(newController.ConnectToolUnlockStateInfos[RailGuid].IsUnlocked);
        }

        private static (global::Server.Protocol.PacketResponseCreator packet, ServiceProvider serviceProvider) CreateServer()
        {
            return new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
        }
    }
}
