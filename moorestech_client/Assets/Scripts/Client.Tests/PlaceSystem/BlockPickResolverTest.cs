using Client.Game.InGame.UI.UIState.State.PlacementPick;
using Common.Debug;
using Core.Master;
using Game.Block.Interface;
using Game.PlacementTarget;
using Game.UnlockState;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Tests.CombinedTest.Server.PacketTest;
using Tests.Module.TestMod;

namespace Client.Tests.PlaceSystem
{
    public class BlockPickResolverTest
    {
        [Test]
        public void 解放済み通常ブロックはピックできる()
        {
            var serviceProvider = CreateServer();
            PlaceBlockProtocolTestSupport.UnlockBlock(serviceProvider, ForUnitTestModBlockId.MachineId);
            var unlockState = serviceProvider.GetService<IGameUnlockStateDataController>();

            Assert.IsTrue(CreateResolver().TryResolvePickTarget(ForUnitTestModBlockId.MachineId, BlockDirection.North, unlockState, out _));
        }

        [Test]
        public void ベルト坂ブロックは直線の解放状態でピックでき手持ちは坂のまま()
        {
            var serviceProvider = CreateServer();
            PlaceBlockProtocolTestSupport.UnlockBlock(serviceProvider, ForUnitTestModBlockId.GearBeltConveyor);
            var unlockState = serviceProvider.GetService<IGameUnlockStateDataController>();

            // 坂は直線の解放状態でピック可（手持ちは坂）
            // A slope is pickable via the straight's unlock state (held as the slope itself)
            Assert.IsTrue(CreateResolver().TryResolvePickTarget(ForUnitTestModBlockId.TestGearBeltConveyorUp, BlockDirection.North, unlockState, out var resolved));
            Assert.AreEqual(MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.TestGearBeltConveyorUp).BlockGuid, resolved.BlockGuid);
        }

        [Test]
        public void 直線が未解放なら坂もピックできない()
        {
            var serviceProvider = CreateServer();
            PlaceBlockProtocolTestSupport.LockBlock(serviceProvider, ForUnitTestModBlockId.GearBeltConveyor);
            var unlockState = serviceProvider.GetService<IGameUnlockStateDataController>();

            Assert.IsFalse(CreateResolver().TryResolvePickTarget(ForUnitTestModBlockId.TestGearBeltConveyorUp, BlockDirection.North, unlockState, out _));
        }

        [Test]
        public void 未解放ブロックはピックできない()
        {
            var serviceProvider = CreateServer();
            PlaceBlockProtocolTestSupport.LockBlock(serviceProvider, ForUnitTestModBlockId.MachineId);
            var unlockState = serviceProvider.GetService<IGameUnlockStateDataController>();

            Assert.IsFalse(CreateResolver().TryResolvePickTarget(ForUnitTestModBlockId.MachineId, BlockDirection.North, unlockState, out _));
        }

        // スポイトもビルドメニューと同じく無料設置デバッグに従う
        // The eyedropper follows the free-placement debug flag just like the build menu
        [Test]
        public void 無料設置デバッグ中は未解放ブロックもピックできる()
        {
            var serviceProvider = CreateServer();
            PlaceBlockProtocolTestSupport.LockBlock(serviceProvider, ForUnitTestModBlockId.MachineId);
            var unlockState = serviceProvider.GetService<IGameUnlockStateDataController>();

            // Client.Tests は隔離済みだが、後続テストへ残さないよう必ず消す
            // Client.Tests is already isolated; still remove it so later tests are unaffected
            DebugParameters.SaveBool(DebugParameterKeys.FreeBlockPlacement, true);
            try
            {
                Assert.IsTrue(CreateResolver().TryResolvePickTarget(ForUnitTestModBlockId.MachineId, BlockDirection.North, unlockState, out _));
            }
            finally
            {
                DebugParameters.RemoveBool(DebugParameterKeys.FreeBlockPlacement);
            }
        }

        private static BlockPickResolver CreateResolver()
        {
            return new BlockPickResolver(new PlacementTargetCatalog());
        }

        private static ServiceProvider CreateServer()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            return serviceProvider;
        }
    }
}
