using System.Linq;
using Client.Game.InGame.UI.UIState.State.PlacementPick;
using Core.Master;
using Game.UnlockState;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;

namespace Client.Tests.PlaceSystem
{
    public class TrainCarPickResolverTest
    {
        [Test]
        public void 解放済み車両はピックできる()
        {
            var serviceProvider = CreateServer();
            var unlockState = serviceProvider.GetService<IGameUnlockStateDataController>();
            var unlockedCarGuid = MasterHolder.TrainUnitMaster.Train.TrainCars.First(c => c.InitialUnlocked == true).TrainCarGuid;

            Assert.IsTrue(TrainCarPickResolver.TryResolvePickTarget(unlockedCarGuid, unlockState, out var resolved));
            Assert.AreEqual(unlockedCarGuid, resolved.TrainCarGuid);
        }

        [Test]
        public void 未解放車両はピックできない()
        {
            var serviceProvider = CreateServer();
            var unlockState = serviceProvider.GetService<IGameUnlockStateDataController>();
            var lockedCarGuid = MasterHolder.TrainUnitMaster.Train.TrainCars.First(c => c.InitialUnlocked != true).TrainCarGuid;

            Assert.IsFalse(TrainCarPickResolver.TryResolvePickTarget(lockedCarGuid, unlockState, out _));
        }

        [Test]
        public void 解放した車両はピックできるようになる()
        {
            var serviceProvider = CreateServer();
            var unlockState = serviceProvider.GetService<IGameUnlockStateDataController>();
            var lockedCarGuid = MasterHolder.TrainUnitMaster.Train.TrainCars.First(c => c.InitialUnlocked != true).TrainCarGuid;

            unlockState.UnlockTrainCar(lockedCarGuid);

            Assert.IsTrue(TrainCarPickResolver.TryResolvePickTarget(lockedCarGuid, unlockState, out var resolved));
            Assert.AreEqual(lockedCarGuid, resolved.TrainCarGuid);
        }

        private static ServiceProvider CreateServer()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            return serviceProvider;
        }
    }
}
