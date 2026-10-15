using System;
using System.Linq;
using Core.Item;
using Core.Master;
using Game.Block.Blocks.ConnectionLine;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;

namespace Tests.UnitTest.Game.Chain
{
    // - 接続線の素材返却
    // - Connection-line material refunds
    // - スタック上限で分割・総数保持
    // - Split at stack limits and preserve totals
    public class ConnectionLineRefundItemsTest
    {
        private static readonly Guid Stack100ItemGuid = Guid.Parse("00000000-0000-0000-1234-000000000004");
        private static readonly Guid Stack50ItemGuid = Guid.Parse("00000000-0000-0000-1234-000000000002");

        [SetUp]
        public void SetUp()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
        }

        [TestCase(230, new[] { 100, 100, 30 })]
        [TestCase(100, new[] { 100 })]
        [TestCase(99, new[] { 99 })]
        public void MaterialAboveMaxStackSplitsIntoMaxSizedStacks(int count, int[] expectedCounts)
        {
            var itemId = MasterHolder.ItemMaster.GetItemId(Stack100ItemGuid);
            Assert.AreEqual(100, ItemStackLevelDataStore.Instance.GetMaxStack(itemId));
            var stacks = ConnectionLineRefundItems.Create(new[] { new ConnectToolMaterialCost(itemId, count) });

            Assert.IsTrue(stacks.All(stack => stack.Id == itemId));
            CollectionAssert.AreEqual(expectedCounts, stacks.Select(stack => stack.Count).ToArray());
        }

        [Test]
        public void EachMaterialSplitsByItsOwnMaxStackAndSkipsEmptyEntries()
        {
            var stack100 = MasterHolder.ItemMaster.GetItemId(Stack100ItemGuid);
            var stack50 = MasterHolder.ItemMaster.GetItemId(Stack50ItemGuid);
            var materials = new[]
            {
                new ConnectToolMaterialCost(stack100, 230),
                new ConnectToolMaterialCost(stack50, 120),
                new ConnectToolMaterialCost(stack50, 0),
                new ConnectToolMaterialCost(ItemMaster.EmptyItemId, 5),
            };
            var stacks = ConnectionLineRefundItems.Create(materials);

            // 素材別の上限で分割・総数保持
            // Split by each material’s limit and preserve totals
            CollectionAssert.AreEqual(new[] { 100, 100, 30 }, stacks.Where(s => s.Id == stack100).Select(s => s.Count).ToArray());
            CollectionAssert.AreEqual(new[] { 50, 50, 20 }, stacks.Where(s => s.Id == stack50).Select(s => s.Count).ToArray());
            Assert.AreEqual(6, stacks.Count);
        }
    }
}
