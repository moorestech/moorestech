using System;
using System.IO;
using Core.Update;
using Game.Paths;
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

        // 同一プロセスで別ワールドを動かした後でも、新規ワールドは前のワールドの状態から続いてはいけない
        // A new world must never continue from the previous world's state, even in the same process
        [Test]
        public void 新規ワールド作成は時刻と乱数を初期化する()
        {
            GameRandom.Reseed(0UL);
            var freshState = GameRandom.ExportState();

            // 前のワールドを動かした後の状態を模す
            // Mimic the state left behind after another world ran
            GameUpdater.RestoreCurrentTick(12345);
            GameRandom.Reseed(999UL);
            GameRandom.NextUlong();

            var savePath = Path.Combine(Path.GetTempPath(), $"moorestech-newworld-{Guid.NewGuid():N}", "save.json");
            var options = new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory)
            {
                worldDataDirectory = WorldDataDirectory.FromServerDataMap(TestModDirectory.ForUnitTestModDirectory, savePath),
            };
            var (_, provider) = new MoorestechServerDIContainerGenerator().Create(options);
            provider.GetRequiredService<IWorldSaveDataLoader>().LoadOrInitialize();

            Assert.AreEqual(0UL, GameUpdater.CurrentTick, "新規ワールドの currentTick が0に初期化されていない");
            CollectionAssert.AreEqual(freshState, GameRandom.ExportState(), "新規ワールドの乱数が初期化されていない");
        }
    }
}
