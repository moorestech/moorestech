using Core.Update;
using Game.SaveLoad.Interface;
using Game.SaveLoad.Json;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;

namespace Tests.CombinedTest.Game
{
    public class SaveTickAndRandomStateTest
    {
        [Test]
        public void tickと乱数状態がセーブロードで一致する()
        {
            var (_, saveProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            GameRandom.Reseed(31337UL);
            GameRandom.NextUlong();
            for (var i = 0; i < 17; i++) GameUpdater.UpdateOneTick();
            var savedTick = GameUpdater.CurrentTick;
            var savedState = GameRandom.ExportState();
            var json = saveProvider.GetRequiredService<AssembleSaveJsonText>().AssembleSaveJson();

            // 別コンテナで状態を乱してからロードし、復元されることを見る
            // Disturb the state in a fresh container, then load and observe restoration
            var (_, loadProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            GameRandom.Reseed(1UL);
            GameUpdater.UpdateOneTick();
            (loadProvider.GetRequiredService<IWorldSaveDataLoader>() as WorldLoaderFromJson).Load(json);

            Assert.AreEqual(savedTick, GameUpdater.CurrentTick, "currentTick が復元されていない");
            CollectionAssert.AreEqual(savedState, GameRandom.ExportState(), "randomState が復元されていない");
        }
    }
}
