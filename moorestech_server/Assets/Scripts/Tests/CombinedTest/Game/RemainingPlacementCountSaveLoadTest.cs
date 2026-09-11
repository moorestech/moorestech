using Game.Construction;
using Game.SaveLoad.Interface;
using Game.SaveLoad.Json;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using Tests.Util;

namespace Tests.CombinedTest.Game
{
    public class RemainingPlacementCountSaveLoadTest
    {
        private const int PlayerId = 0;

        [Test]
        public void セーブしてロードすると残り設置数が復元される()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var wallet = ForUnitTestModBlockId.GearBeltConveyor;
            RemainingPlacementCountTestState.SetRemainingCount(serviceProvider, PlayerId, wallet, 2);
            var saveJson = serviceProvider.GetService<AssembleSaveJsonText>().AssembleSaveJson();

            var (_, loadServiceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            (loadServiceProvider.GetService<IWorldSaveDataLoader>() as WorldLoaderFromJson).Load(saveJson);

            Assert.AreEqual(2, loadServiceProvider.GetService<IRemainingPlacementCountLookup>().GetRemainingCount(PlayerId, wallet));
        }
    }
}
