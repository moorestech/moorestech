using Game.Construction;
using Game.SaveLoad.Interface;
using Game.SaveLoad.Json;
using Microsoft.Extensions.DependencyInjection;
using Game.PlayerIdentity;
using NUnit.Framework;
using Tests.Util.PlayerIdentity;
using Server.Boot;
using Tests.Module.TestMod;

namespace Tests.CombinedTest.Game
{
    public class RemainingPlacementCountSaveLoadTest
    {

        [Test]
        public void セーブしてロードすると残り設置数が復元される()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var playerId = PlayerIdentityTestHelper.Register(serviceProvider.GetRequiredService<IPlayerIdentityRegistry>(), "steam:1").PlayerId;
            var store = serviceProvider.GetService<RemainingPlacementCountDataStore>();
            var wallet = ForUnitTestModBlockId.GearBeltConveyor;
            store.Refill(playerId, wallet, 3);
            store.ConsumeOne(playerId, wallet);
            var saveJson = serviceProvider.GetService<AssembleSaveJsonText>().AssembleSaveJson();

            var (_, loadServiceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            (loadServiceProvider.GetService<IWorldSaveDataLoader>() as WorldLoaderFromJson).Load(saveJson);

            Assert.AreEqual(2, loadServiceProvider.GetService<IRemainingPlacementCountLookup>().GetRemainingCount(playerId, wallet));
        }
    }
}
