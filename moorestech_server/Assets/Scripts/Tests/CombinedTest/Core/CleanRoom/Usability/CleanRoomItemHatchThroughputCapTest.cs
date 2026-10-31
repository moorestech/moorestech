using System.IO;
using System.Linq;
using Core.Master;
using Core.Master.Validator;
using Core.Update;
using Game.Block.Blocks.Chest;
using Game.Block.Interface.Component;
using Game.Block.Interface.Extension;
using Game.Context;
using Mooresmaster.Model.BlocksModule;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;

namespace Tests.CombinedTest.Core.CleanRoom.Usability
{
    public class CleanRoomItemHatchThroughputCapTest
    {
        private const int MeasuredSeconds = 5;
        private const int MeasuredTicks = MeasuredSeconds * GameUpdater.TicksPerSecond;

        [Test]
        public void OverCapInflowIsCappedPerSecondTest()
        {
            CleanRoomHatchTest.CreateServer();
            var cap = ReadTestHatchCap();

            // ハッチの出口（北）にチェストを置き、毎tick3個を直接押し込んで差し戻し量を数える
            // Put a chest at the hatch's north exit, push three items in every tick and count what bounces back
            var hatch = CleanRoomHatchTest.PlaceBlock(ForUnitTestModBlockId.CleanRoomItemHatchId, new Vector3Int(0, 0, 0));
            var chest = CleanRoomHatchTest.PlaceBlock(ForUnitTestModBlockId.ChestId, new Vector3Int(0, 0, 1)).GetComponent<VanillaChestComponent>();
            var hatchInventory = hatch.GetComponent<IBlockInventory>();
            var bouncedTotal = 0;
            for (var i = 0; i < MeasuredTicks; i++)
            {
                bouncedTotal += hatchInventory.InsertItem(ServerContext.ItemStackFactory.Create(new ItemId(1), 3), InsertItemContext.Empty).Count;
                GameUpdater.UpdateOneTick();
            }

            // 通過量は上限×秒数以上、それに最初に貯まっている1秒分を足した量以下に収まる
            // Passed items are at least cap x seconds and at most that plus the one second of allowance stored at the start
            var passed = chest.InventoryItems.Sum(stack => stack.Count);
            Assert.That(passed, Is.InRange((int)(cap * MeasuredSeconds), (int)(cap * (MeasuredSeconds + 1))));
            Assert.LessOrEqual(hatch.GetComponent<ICleanRoomItemHatch>().RecentThroughputPerSecond, cap + 1e-6);

            // 通れなかった分は中継スロットに残り、満杯のハッチは既存どおり挿入を全量差し戻す
            // What could not pass stays in the transit slots, and the full hatch hands inserts back whole as before
            var inTransit = hatch.GetComponent<IGetRefundItemsInfo>().GetRefundItems().Sum(stack => stack.Count);
            Assert.Greater(bouncedTotal, 0);
            Assert.AreEqual(3 * MeasuredTicks - bouncedTotal, passed + inTransit);
        }

        [Test]
        public void WithinCapInflowAllPassesTest()
        {
            CleanRoomHatchTest.CreateServer();

            // 2tickに1個（毎秒10個）はテストマスタの上限を下回る
            // One item every two ticks (10 per second) is below the test master's cap
            Assert.Greater(ReadTestHatchCap(), GameUpdater.TicksPerSecond / 2.0);
            var hatch = CleanRoomHatchTest.PlaceBlock(ForUnitTestModBlockId.CleanRoomItemHatchId, new Vector3Int(0, 0, 0));
            var chest = CleanRoomHatchTest.PlaceBlock(ForUnitTestModBlockId.ChestId, new Vector3Int(0, 0, 1)).GetComponent<VanillaChestComponent>();
            var hatchInventory = hatch.GetComponent<IBlockInventory>();
            for (var i = 0; i < MeasuredTicks; i++)
            {
                if (i % 2 == 0) hatchInventory.InsertItem(ServerContext.ItemStackFactory.Create(new ItemId(1), 1), InsertItemContext.Empty);
                GameUpdater.UpdateOneTick();
            }

            // 上限内の流れは全量がチェストへ届き、中継スロットに何も残らない
            // A flow within the cap reaches the chest in full and leaves nothing in the transit slots
            Assert.AreEqual(MeasuredTicks / 2, chest.InventoryItems.Sum(stack => stack.Count));
            Assert.AreEqual(0, hatch.GetComponent<IGetRefundItemsInfo>().GetRefundItems().Count);
        }

        [TestCase(0.0)]
        [TestCase(-1.0)]
        [TestCase(double.NaN)]
        public void NonPositiveCapIsRejectedByValidationTest(double maxItemsPerSecond)
        {
            var blocksJToken = PrepareBlocksJson();
            FindBlockParam(blocksJToken, "TestCleanRoomItemHatch")["maxItemsPerSecond"] = maxItemsPerSecond;

            var isValid = BlockMasterUtil.Validate(new BlockMaster(blocksJToken).Blocks, out var errorLogs);

            Assert.IsFalse(isValid);
            StringAssert.Contains($"[BlockMaster] Name:TestCleanRoomItemHatch has maxItemsPerSecond:{(float)maxItemsPerSecond} that is not positive", errorLogs);
        }

        // int.MaxValueはfloatで2^31へ丸まり搬送側のint換算をあふれるので、無限大・3e9と同じく拒否する
        // int.MaxValue rounds to 2^31 as float and overflows the hatch's int conversion, so it is rejected like infinity and 3e9
        [TestCase(2147483647.0)]
        [TestCase(3e9)]
        [TestCase(double.PositiveInfinity)]
        public void CapAboveIntRangeIsRejectedByValidationTest(double maxItemsPerSecond)
        {
            var blocksJToken = PrepareBlocksJson();
            FindBlockParam(blocksJToken, "TestCleanRoomItemHatch")["maxItemsPerSecond"] = maxItemsPerSecond;

            var isValid = BlockMasterUtil.Validate(new BlockMaster(blocksJToken).Blocks, out var errorLogs);

            Assert.IsFalse(isValid);
            StringAssert.Contains($"[BlockMaster] Name:TestCleanRoomItemHatch has maxItemsPerSecond:{(float)maxItemsPerSecond} that exceeds int.MaxValue", errorLogs);
        }

        // 2147483520はint.MaxValue以下で最大のfloat
        // 2147483520 is the largest float not above int.MaxValue
        [TestCase(0.5)]
        [TestCase(2147483520.0)]
        public void PositiveCapWithinIntRangePassesValidationTest(double maxItemsPerSecond)
        {
            var blocksJToken = PrepareBlocksJson();
            FindBlockParam(blocksJToken, "TestCleanRoomItemHatch")["maxItemsPerSecond"] = maxItemsPerSecond;

            var isValid = BlockMasterUtil.Validate(new BlockMaster(blocksJToken).Blocks, out var errorLogs);

            Assert.IsTrue(isValid, errorLogs);
        }

        private static double ReadTestHatchCap()
        {
            return ((CleanRoomItemHatchBlockParam)MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.CleanRoomItemHatchId).BlockParam).MaxItemsPerSecond;
        }

        private static JToken PrepareBlocksJson()
        {
            // 依存マスタを初期化し、テスト内のJTokenだけを書き換える
            // Initialize the dependency masters, then edit only the in-test JToken
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var blocksJsonPath = Path.Combine(TestModDirectory.ForUnitTestModDirectory, "mods", "forUnitTest", "master", "blocks.json");
            return JToken.Parse(File.ReadAllText(blocksJsonPath));
        }

        private static JToken FindBlockParam(JToken blocksJToken, string blockName)
        {
            return blocksJToken["data"].Children<JObject>().Single(block => (string)block["name"] == blockName)["blockParam"];
        }
    }
}
