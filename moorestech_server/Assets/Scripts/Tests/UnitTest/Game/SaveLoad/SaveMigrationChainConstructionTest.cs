using System;
using System.Collections.Generic;
using Game.SaveLoad.Migration;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Tests.UnitTest.Game.SaveLoad
{
    /// <summary>連鎖の構築時検証。欠番・重複は起動時に落として、版の恒久封鎖を作らない</summary>
    /// <summary>The chain's construction-time validation; gaps and duplicates fail at boot so no version is permanently locked out</summary>
    public class SaveMigrationChainConstructionTest
    {
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

        // 構築だけを見るので変換は何もしない。FromVersionの並びだけが検証対象
        // Construction is all that is exercised here, so the step converts nothing; only the FromVersion sequence matters
        private sealed class RecordingStep : ISaveMigrationStep
        {
            public RecordingStep(int fromVersion, List<int> applied)
            {
                FromVersion = fromVersion;
                _applied = applied;
            }

            private readonly List<int> _applied;

            public int FromVersion { get; }

            public SaveMigrationStepResult Migrate(JObject save)
            {
                _applied.Add(FromVersion);
                return SaveMigrationStepResult.Converted(save);
            }
        }
    }
}
