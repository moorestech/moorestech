using Game.Construction;
using Game.SaveLoad.Interface;
using Game.SaveLoad.Json;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;

namespace Tests.CombinedTest.Game
{
    public class RemainingPlacementCountSaveLoadTest
    {
        private const int PlayerId = 0;

        [Test]
        public void セーブしてロードすると残り設置数が復元される()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var store = serviceProvider.GetService<RemainingPlacementCountDataStore>();
            var wallet = ForUnitTestModBlockId.GearBeltConveyor;
            store.Refill(PlayerId, wallet, 3);
            store.ConsumeOne(PlayerId, wallet);
            var saveJson = serviceProvider.GetService<AssembleSaveJsonText>().AssembleSaveJson();

            var (_, loadServiceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            (loadServiceProvider.GetService<IWorldSaveDataLoader>() as WorldLoaderFromJson).Load(saveJson);

            Assert.AreEqual(2, loadServiceProvider.GetService<IRemainingPlacementCountLookup>().GetRemainingCount(PlayerId, wallet));
        }
    }
}
