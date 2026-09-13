using System;
using System.IO;
using Core.Update;
using Game.Map;
using Game.Paths;
using Game.SaveLoad.Interface;
using Game.SaveLoad.Json;
using Game.SaveLoad.Json.WorldVersions;
using Microsoft.Extensions.DependencyInjection;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests.CombinedTest.Game
{
    public class SaveTickAndRandomStateTest
    {
        private const int PlayerId = 7;
        private const double AttackSpeedSeconds = 2.0;


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

        // 版1の実セーブには3項目がそもそも無い。V1→V2ステップが補うので、前段を通せばロードは通る
        // A real version 1 save has none of the three fields; the V1-to-V2 step backfills them so the prepared save loads
        // 補填値は「時刻0・種0の乱数列・クールダウン無し」であり、この3つが今の正しい挙動そのもの
        // The backfilled values are tick 0, the seed-0 random stream and no cooldown, and those three are the correct behaviour now
        [Test]
        public void 版1のセーブは3項目が欠けていても補填されて実ロードできる()
        {
            var save = SaveLoadPreparerTestFixture.BuildSaveJson();
            save["worldVersion"] = 1;
            save.Remove("currentTick");
            save.Remove("randomState");
            save.Remove("miningCooldowns");

            var archiveRoot = SaveLoadPreparerTestFixture.ArchiveRootForThisRun();
            var (_, preparer) = SaveLoadPreparerTestFixture.CreatePreparer(archiveRoot);
            var prepared = preparer.Prepare(save.ToString());
            Assert.IsTrue(prepared.CanLoad, prepared.BlockedReason);

            var loader = SaveLoadPreparerTestFixture.CreateContainer().GetService<IWorldSaveDataLoader>() as WorldLoaderFromJson;

            // 補填値と紛れないよう、ロード直前に別の時刻と乱数へ動かしておく
            // Move to a different clock and stream right before load so the backfilled values cannot be mistaken for leftovers
            GameUpdater.RestoreCurrentTick(4242);
            GameRandom.Reseed(999UL);

            Assert.DoesNotThrow(() => loader.Load(prepared.SaveJsonText));

            Assert.AreEqual(0UL, GameUpdater.CurrentTick, "補填したcurrentTickが0で復元されていない");
            CollectionAssert.AreEqual(GameRandom.StateFromSeed(0UL), GameRandom.ExportState(), "補填したrandomStateが種0の状態になっていない");

            if (Directory.Exists(archiveRoot)) Directory.Delete(archiveRoot, true);
        }

        // currentTick は値型なので欠損しても既定の0で通り、tickが無音で巻き戻ったまま再生が始まる
        // currentTick is a value type, so a missing field passes as the default 0 and replay starts from a silently rewound clock
        // 補填は版1からの変換の仕事なので、現在版で欠けているのは手編集の破損であり例外で止める
        // Backfilling belongs to the version 1 migration, so a field missing at the current version is hand-edited corruption and must throw
        [Test]
        public void currentTickが無い現在版のセーブは無音で0にせず落とす()
        {
            var (_, provider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            GameUpdater.RestoreCurrentTick(555);
            var json = provider.GetRequiredService<AssembleSaveJsonText>().AssembleSaveJson();

            var root = JObject.Parse(json);
            Assert.AreEqual(WorldSaveAllInfoV1.CurrentVersion, root["worldVersion"].Value<int>(), "この検証は現在版のセーブが前提");
            root.Remove("currentTick");

            var loader = provider.GetRequiredService<IWorldSaveDataLoader>() as WorldLoaderFromJson;
            Assert.Throws<InvalidOperationException>(() => loader.Load(root.ToString()));
            Assert.AreEqual(555UL, GameUpdater.CurrentTick, "欠損したセーブのロードでtickが0へ巻き戻っている");
        }

        // miningCooldowns は参照型で、欠損をそのまま通すと復元先で素のNullReferenceExceptionになり理由が残らない
        // miningCooldowns is a reference type; letting a missing one through gives a bare NullReferenceException with no reason
        [Test]
        public void miningCooldownsが無い現在版のセーブは理由付きで落とす()
        {
            var (_, provider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var json = provider.GetRequiredService<AssembleSaveJsonText>().AssembleSaveJson();

            var root = JObject.Parse(json);
            Assert.AreEqual(WorldSaveAllInfoV1.CurrentVersion, root["worldVersion"].Value<int>(), "この検証は現在版のセーブが前提");
            root.Remove("miningCooldowns");

            LogAssert.Expect(LogType.Error, new Regex("^セーブに miningCooldowns がありません"));

            var loader = provider.GetRequiredService<IWorldSaveDataLoader>() as WorldLoaderFromJson;
            var exception = Assert.Throws<InvalidOperationException>(() => loader.Load(root.ToString()));
            StringAssert.Contains("miningCooldowns", exception.Message);
            StringAssert.Contains("マイグレーション連鎖", exception.Message);
        }

        // クールダウンを保存しないと、ロード直後の再生が保存前の世界では拒否された採掘を通して発散する
        // Without a saved cooldown, a replay right after load accepts mining the pre-save world rejected and diverges
        [Test]
        public void 採掘クールダウンがセーブロードで復元される()
        {
            var (_, saveProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            GameUpdater.RestoreCurrentTick(1000);
            saveProvider.GetRequiredService<MiningCooldownService>().RecordAttack(PlayerId);
            var json = saveProvider.GetRequiredService<AssembleSaveJsonText>().AssembleSaveJson();

            var (_, loadProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var cooldownService = loadProvider.GetRequiredService<MiningCooldownService>();
            Assert.IsFalse(cooldownService.IsInCooldown(PlayerId, AttackSpeedSeconds), "ロード前から採掘クールダウンが載っている");

            (loadProvider.GetRequiredService<IWorldSaveDataLoader>() as WorldLoaderFromJson).Load(json);
            Assert.IsTrue(cooldownService.IsInCooldown(PlayerId, AttackSpeedSeconds), "最終採掘tickが復元されていない");

            // クールダウンが明けるところまで進めれば、復元した値が時刻と噛み合っていることまで見える
            // Advancing past the cooldown shows the restored value actually lines up with the clock
            GameUpdater.RestoreCurrentTick(GameUpdater.CurrentTick + GameUpdater.SecondsToTicks(AttackSpeedSeconds));
            Assert.IsFalse(cooldownService.IsInCooldown(PlayerId, AttackSpeedSeconds), "復元したクールダウンが明けない");
        }
    }
}
