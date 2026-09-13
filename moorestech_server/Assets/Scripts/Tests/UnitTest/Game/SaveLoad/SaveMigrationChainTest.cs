using System;
using System.Collections.Generic;
using System.IO;
using Game.Paths;
using Game.SaveLoad.Json.WorldVersions;
using Game.SaveLoad.Migration;
using Game.SaveLoad.Migration.Steps;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Tests.UnitTest.Game.SaveLoad
{
    public class SaveMigrationChainTest
    {
        // 記録順ではなくFromVersion昇順で適用されることを見るため、登録順をわざと逆にする
        // Register out of order so the test proves ordering comes from FromVersion, not registration
        [Test]
        public void 版を跨いだ連鎖が昇順に適用されるTest()
        {
            var applied = new List<int>();
            var chain = new SaveMigrationChain(new ISaveMigrationStep[]
            {
                new RecordingStep(2, applied),
                new RecordingStep(1, applied),
            }, 3);

            var result = chain.Migrate(JObject.Parse("{\"worldVersion\":1}"));

            Assert.IsTrue(result.CanLoad, result.BlockedReason);
            Assert.IsTrue(result.Migrated);
            Assert.AreEqual(new[] { 1, 2 }, applied.ToArray());
            Assert.AreEqual(3, result.Save["worldVersion"].Value<int>());
            Assert.AreEqual("1:2:", result.Save["trace"].Value<string>());
        }

        // 途中版セーブにはその版以降のみ適用
        // A save starting at an intermediate version gets only the steps at or above that version
        [Test]
        public void 途中の版のセーブには残りのステップだけが適用されるTest()
        {
            var applied = new List<int>();
            var chain = new SaveMigrationChain(new ISaveMigrationStep[]
            {
                new RecordingStep(1, applied),
                new RecordingStep(2, applied),
            }, 3);

            var result = chain.Migrate(JObject.Parse("{\"worldVersion\":2}"));

            Assert.AreEqual(new[] { 2 }, applied.ToArray());
            Assert.AreEqual(3, result.Save["worldVersion"].Value<int>());
        }

        // worldVersionが無いセーブは最古の版とみなす。将来版と取り違えると原本を壊すので固定する
        // A save without worldVersion is treated as the oldest version; mistaking it for a future one would destroy the original
        [Test]
        public void 版キーが無いセーブは版1として扱うTest()
        {
            var result = new SaveMigrationChain(Array.Empty<ISaveMigrationStep>(), 1).Migrate(JObject.Parse("{}"));

            Assert.AreEqual(1, result.FromVersion);
        }

        // 現在版セーブは実連鎖でも無変換で通る
        // A save already at the current version passes through the real production chain untouched
        [Test]
        public void 現在版のセーブはステップが当たらずそのまま通るTest()
        {
            var save = JObject.Parse($"{{\"worldVersion\":{WorldSaveAllInfoV1.CurrentVersion}}}");

            var result = CurrentVersionChain().Migrate(save);

            Assert.IsTrue(result.CanLoad, result.BlockedReason);
            Assert.IsFalse(result.Migrated);
            Assert.AreEqual(WorldSaveAllInfoV1.CurrentVersion, result.Save["worldVersion"].Value<int>());
        }

        [Test]
        public void 未来版のセーブはロードを拒否し理由に版番号を含むTest()
        {
            var result = CurrentVersionChain().Migrate(JObject.Parse("{\"worldVersion\":999}"));

            Assert.IsFalse(result.CanLoad);
            StringAssert.Contains("999", result.BlockedReason);
        }

        [Test]
        public void 版0以下のセーブはロードを拒否するTest()
        {
            Assert.IsFalse(CurrentVersionChain().Migrate(JObject.Parse("{\"worldVersion\":0}")).CanLoad);
        }

        // 欠番が無い(1,2は揃っている)状態で重複だけを混ぜ、重複検知そのものを見る
        // No gap (1 and 2 are both present) so only the duplicate is exercised
        [Test]
        public void FromVersionが重複した連鎖は構築時に落ちるTest()
        {
            var applied = new List<int>();
            Assert.Throws<ArgumentException>(() => new SaveMigrationChain(new ISaveMigrationStep[]
            {
                new RecordingStep(1, applied),
                new RecordingStep(1, applied),
                new RecordingStep(2, applied),
            }, 3));
        }

        // 目標版に対してステップが多すぎる(範囲外のFromVersionを含む)場合も構築時に落ちる
        // Too many steps for the target version (an out-of-range FromVersion) also fails at construction
        [Test]
        public void 目標版に対してステップが多すぎる連鎖は構築時に落ちるTest()
        {
            var applied = new List<int>();
            Assert.Throws<ArgumentException>(() => new SaveMigrationChain(new ISaveMigrationStep[]
            {
                new RecordingStep(1, applied),
                new RecordingStep(2, applied),
            }, 2));
        }

        // 欠番は「その版のセーブが永久にロードできない」という恒久封鎖なので構築時に落とす
        // A gap permanently blocks that version's saves, so it fails at construction instead of at load time
        [Test]
        public void FromVersionに欠番がある連鎖は構築時に落ちるTest()
        {
            var applied = new List<int>();
            Assert.Throws<ArgumentException>(() => new SaveMigrationChain(new ISaveMigrationStep[]
            {
                new RecordingStep(1, applied),
                new RecordingStep(3, applied),
            }, 4));
        }

        // 目標版が1のときだけ空連鎖が成立する。現在版が上がったら空連鎖は構築時に落ちる
        // Only a target version of 1 admits an empty chain; once the current version rises an empty chain fails at construction
        [Test]
        public void 目標版1の連鎖はステップ0本で構築できるTest()
        {
            Assert.DoesNotThrow(() => new SaveMigrationChain(Array.Empty<ISaveMigrationStep>(), 1));
        }

        [Test]
        public void バックアップは既存の原本を上書きしないTest()
        {
            var root = Path.Combine(Path.GetTempPath(), "moorestech-save-archive-" + Guid.NewGuid().ToString("N"));
            var writer = new SaveArchiveWriter(SaveArchiveDirectory.FromArchiveRoot(root));

            writer.WriteBackup(1, "{\"first\":true}");
            writer.WriteBackup(1, "{\"second\":true}");

            var path = SaveArchiveDirectory.FromArchiveRoot(root).BackupSaveJsonPath(1);
            Assert.AreEqual("{\"first\":true}", File.ReadAllText(path));
            Directory.Delete(root, true);
        }

        // 同秒に2回除去が起きても片方が消えないことを見る
        // Two prunes in the same second must not overwrite each other
        [Test]
        public void 除去データは同秒でも連番で別ファイルになるTest()
        {
            var root = Path.Combine(Path.GetTempPath(), "moorestech-save-archive-" + Guid.NewGuid().ToString("N"));
            var directory = SaveArchiveDirectory.FromArchiveRoot(root);
            var writer = new SaveArchiveWriter(directory);
            var at = new DateTime(2026, 9, 13, 8, 30, 0, DateTimeKind.Utc);

            writer.WritePruned(JObject.Parse("{\"a\":1}"), at);
            writer.WritePruned(JObject.Parse("{\"a\":2}"), at);

            Assert.AreEqual(2, Directory.GetFiles(directory.PrunedRoot, "*.json").Length);
            Assert.IsTrue(File.Exists(directory.PrunedJsonPath(at, 0)));
            Assert.IsTrue(File.Exists(directory.PrunedJsonPath(at, 1)));
            Directory.Delete(root, true);
        }

        // 本番と同じ構成の連鎖。現在版と実ステップ列の噛み合いもここで一緒に検証される
        // The production configuration; building it also verifies the real step set matches the current version
        private static SaveMigrationChain CurrentVersionChain()
        {
            return new SaveMigrationChain(new ISaveMigrationStep[] { new SaveMigrationStepV1ToV2() }, WorldSaveAllInfoV1.CurrentVersion);
        }

        // テスト専用の疑似ステップ。適用順の記録とJSONへの痕跡付けだけを行う
        // A test-only fake step that records the order and leaves a trace in the JSON
        private sealed class RecordingStep : ISaveMigrationStep
        {
            private readonly List<int> _applied;

            public RecordingStep(int fromVersion, List<int> applied)
            {
                FromVersion = fromVersion;
                _applied = applied;
            }

            public int FromVersion { get; }

            public JObject Migrate(JObject save)
            {
                _applied.Add(FromVersion);
                save["trace"] = save["trace"] == null ? $"{FromVersion}:" : save["trace"].Value<string>() + $"{FromVersion}:";
                return save;
            }
        }
    }
}
