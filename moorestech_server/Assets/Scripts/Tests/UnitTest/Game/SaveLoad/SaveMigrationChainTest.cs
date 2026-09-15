using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Game.SaveLoad.Json.WorldVersions;
using Game.SaveLoad.Migration;
using Game.SaveLoad.Migration.Steps;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

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

        // 変換できなかった手を握り潰すと、未変換のセーブに新版の版番号だけが刻まれてLoadへ渡る
        // Swallowing a hop that could not convert would stamp the new version onto an unconverted save and pass it to Load
        [Test]
        public void 変換できなかったステップは版を刻まずロードを拒否するTest()
        {
            var save = JObject.Parse("{\"worldVersion\":1}");
            LogAssert.Expect(LogType.Error, new Regex("変換できませんでした"));

            var result = new SaveMigrationChain(new ISaveMigrationStep[] { new FailingStep(1) }, 2).Migrate(save);

            Assert.IsFalse(result.CanLoad);
            StringAssert.Contains(FailingStep.Reason, result.BlockedReason);
            Assert.AreEqual(1, result.FromVersion);
            Assert.AreEqual(1, save["worldVersion"].Value<int>());
        }

        // 2手目が失敗したときも、1手目が刻んだ版のまま中断してLoadへ渡さない
        // When the second hop fails the chain stops with the version the first hop stamped and never reaches Load
        [Test]
        public void 途中の手が失敗した連鎖は最後まで進まないTest()
        {
            var applied = new List<int>();
            LogAssert.Expect(LogType.Error, new Regex("変換できませんでした"));

            var result = new SaveMigrationChain(new ISaveMigrationStep[]
            {
                new RecordingStep(1, applied),
                new FailingStep(2),
            }, 3).Migrate(JObject.Parse("{\"worldVersion\":1}"));

            Assert.IsFalse(result.CanLoad);
            Assert.AreEqual(new[] { 1 }, applied.ToArray());
        }

        // 本番と同じ構成の連鎖。現在版と実ステップ列の噛み合いもここで一緒に検証される
        // The production configuration; building it also verifies the real step set matches the current version
        private static SaveMigrationChain CurrentVersionChain()
        {
            return SaveMigrationChain.ForCurrentVersion(new ISaveMigrationStep[] { new SaveMigrationStepV1ToV2() });
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

            public SaveMigrationStepResult Migrate(JObject save)
            {
                _applied.Add(FromVersion);
                save["trace"] = save["trace"] == null ? $"{FromVersion}:" : save["trace"].Value<string>() + $"{FromVersion}:";
                return SaveMigrationStepResult.Converted(save);
            }
        }

        // 変換できなかったことだけを返すステップ。連鎖が版を刻まずに止まるかを見る
        // A step that only reports it could not convert; used to check the chain stops without stamping a version
        private sealed class FailingStep : ISaveMigrationStep
        {
            public const string Reason = "テスト用の変換不能";

            public FailingStep(int fromVersion)
            {
                FromVersion = fromVersion;
            }

            public int FromVersion { get; }

            public SaveMigrationStepResult Migrate(JObject save)
            {
                return SaveMigrationStepResult.Failed(Reason);
            }
        }
    }
}
