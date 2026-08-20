using Game.UnlockState;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UniRx;

namespace Tests.CombinedTest.Game
{
    public class BlueprintUnlockStateTest
    {
        [Test]
        public void テストマスタのinitialUnlockedがfalseなので初期状態は未解放()
        {
            var (_, serviceProvider) = CreateServer();
            var controller = serviceProvider.GetService<IGameUnlockStateDataController>();

            Assert.IsFalse(controller.IsBlueprintUnlocked);
        }

        [Test]
        public void ブループリント解放が保存とロードで維持される()
        {
            var (_, serviceProvider) = CreateServer();
            var controller = serviceProvider.GetService<IGameUnlockStateDataController>();

            // 解放イベントの発火も同時に検証する
            // Verify the unlock event also fires
            var unlockEventCount = 0;
            controller.OnUnlockBlueprint.Subscribe(_ => unlockEventCount++);
            controller.UnlockBlueprint();

            Assert.AreEqual(1, unlockEventCount);
            Assert.IsTrue(controller.IsBlueprintUnlocked);

            // 二重解放はイベントを再発火しない
            // Unlocking twice never re-fires the event
            controller.UnlockBlueprint();
            Assert.AreEqual(1, unlockEventCount);

            // 別サーバーで状態引継ぎ確認
            // Load into another server instance and check the state carries over
            var saveJson = controller.GetSaveJsonObject();
            var (_, newServiceProvider) = CreateServer();
            var newController = newServiceProvider.GetService<IGameUnlockStateDataController>();
            newController.LoadUnlockState(saveJson);

            Assert.IsTrue(newController.IsBlueprintUnlocked);
        }

        [Test]
        public void unlockBlueprintのgameActionで解放される()
        {
            var (_, serviceProvider) = CreateServer();
            var controller = serviceProvider.GetService<IGameUnlockStateDataController>();
            var executor = serviceProvider.GetService<global::Game.Action.IGameActionExecutor>();

            // unlockBlueprintを実行
            // Build and execute the unlockBlueprint action directly
            var action = new Mooresmaster.Model.GameActionModule.GameActionElement(
                0,
                Mooresmaster.Model.GameActionModule.GameActionElement.GameActionTypeConst.unlockBlueprint,
                new Mooresmaster.Model.GameActionModule.UnlockBlueprintGameActionParam());
            executor.ExecuteUnlockActions(new[] { action });

            Assert.IsTrue(controller.IsBlueprintUnlocked);
        }

        [Test]
        public void 旧セーブのように項目が欠損していればシード値のまま未解放()
        {
            var (_, serviceProvider) = CreateServer();
            var controller = serviceProvider.GetService<IGameUnlockStateDataController>();

            // 旧セーブ相当（状態null）
            // Old-save equivalent: load JSON whose BlueprintUnlockState is null
            var saveJson = controller.GetSaveJsonObject();
            saveJson.BlueprintUnlockState = null;
            controller.LoadUnlockState(saveJson);

            Assert.IsFalse(controller.IsBlueprintUnlocked);
        }

        private static (global::Server.Protocol.PacketResponseCreator packet, ServiceProvider serviceProvider) CreateServer()
        {
            return new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
        }
    }
}
