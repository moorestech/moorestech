using System.Text.RegularExpressions;
using Core.Update;
using Game.Block.Interface;
using Game.Context;
using Game.World.Interface.DataStore;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests.CombinedTest.Core
{
    public class GearOverloadTest
    {
        [SetUp]
        public void SetUp()
        {
            // テストごとにアップデーターを初期化
            // Reset updater before each test
            GameUpdater.ResetUpdate();
        }

        [Test]
        public void OverloadRemovesBlock_WhenProbabilityIsCertain()
        {
            // 環境準備: 破壊確率1.0のギアと隣接ジェネレーターを配置
            // Setup gear with guaranteed breakage and adjacent generator
            UnityEngine.Random.InitState(0);
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var world = ServerContext.WorldBlockDatastore;

            world.TryAddBlock(ForUnitTestModBlockId.SmallGear, Vector3Int.zero, BlockDirection.North, System.Array.Empty<BlockCreateParam>(), out _);
            world.TryAddBlock(ForUnitTestModBlockId.SimpleGearGenerator, new Vector3Int(1, 0, 0), BlockDirection.East, System.Array.Empty<BlockCreateParam>(), out _);

            LogAssert.Expect(LogType.Log, new Regex("Block removal: Position=.*Type=Broken"));

            AdvanceTimeSeconds(2f);

            Assert.IsFalse(world.Exists(Vector3Int.zero));
        }

        [Test]
        public void OverloadIsDisabled_WhenLimitsAreZero()
        {
            // 環境準備: 過負荷無効のギアを配置
            // Setup gear with overload checks disabled
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var world = ServerContext.WorldBlockDatastore;

            world.TryAddBlock(ForUnitTestModBlockId.BigGear, Vector3Int.zero, BlockDirection.North, System.Array.Empty<BlockCreateParam>(), out _);
            world.TryAddBlock(ForUnitTestModBlockId.SimpleGearGenerator, new Vector3Int(1, 0, 0), BlockDirection.East, System.Array.Empty<BlockCreateParam>(), out _);

            AdvanceTimeSeconds(3f);

            Assert.IsNotNull(world.GetBlock(Vector3Int.zero));
        }

        private static void AdvanceTimeSeconds(float seconds)
        {
            // 指定秒数分のゲーム更新を進める
            // Advance GameUpdater by specified seconds
            var remaining = seconds;
            while (remaining > 0f)
            {
                GameUpdater.SpecifiedDeltaTimeUpdate(0.2f);
                remaining -= 0.2f;
            }
        }
    }
}
