using Client.Game.InGame.UI.UIState.State.PlacementPick;
using Core.Master;
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
        public void 解放済み通常ブロックはそのまま解決される()
        {
            var serviceProvider = CreateServer();
            PlaceBlockProtocolTestSupport.UnlockBlock(serviceProvider, ForUnitTestModBlockId.MachineId);
            var unlockState = serviceProvider.GetService<IGameUnlockStateDataController>();

            Assert.IsTrue(BlockPickResolver.TryResolvePickTarget(ForUnitTestModBlockId.MachineId, unlockState, out var resolved));
            Assert.AreEqual(ForUnitTestModBlockId.MachineId, resolved);
        }

        [Test]
        public void ベルト隠しバリアントは代表ブロックへ解決される()
        {
            var serviceProvider = CreateServer();
            PlaceBlockProtocolTestSupport.UnlockBlock(serviceProvider, ForUnitTestModBlockId.GearBeltConveyor);
            var unlockState = serviceProvider.GetService<IGameUnlockStateDataController>();

            // 長さ3の隠しバリアントをピックしても代表（長さ1）に解決される
            // Picking the hidden length-3 variant resolves to the representative length-1 block
            Assert.IsTrue(BlockPickResolver.TryResolvePickTarget(ForUnitTestModBlockId.GearBeltConveyor3, unlockState, out var resolved));
            Assert.AreEqual(ForUnitTestModBlockId.GearBeltConveyor, resolved);
        }

        [Test]
        public void 未解放ブロックはピックできない()
        {
            var serviceProvider = CreateServer();
            PlaceBlockProtocolTestSupport.LockBlock(serviceProvider, ForUnitTestModBlockId.MachineId);
            var unlockState = serviceProvider.GetService<IGameUnlockStateDataController>();

            Assert.IsFalse(BlockPickResolver.TryResolvePickTarget(ForUnitTestModBlockId.MachineId, unlockState, out _));
        }

        private static ServiceProvider CreateServer()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            return serviceProvider;
        }
    }
}
