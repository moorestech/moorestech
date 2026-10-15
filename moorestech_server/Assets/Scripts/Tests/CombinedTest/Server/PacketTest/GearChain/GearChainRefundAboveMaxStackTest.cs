using System;
using System.Linq;
using Core.Item;
using Core.Master;
using Game.Block.Interface.Component;
using Game.Context;
using NUnit.Framework;
using static Server.Protocol.PacketResponse.GearChainConnectionEditProtocol;
using static Server.Protocol.PacketResponse.RemoveBlockProtocol;
using static Tests.CombinedTest.Server.PacketTest.GearChain.GearChainEditTestWorld;

namespace Tests.CombinedTest.Server.PacketTest.GearChain
{
    // MaxStackを超える素材記録の撤去・切断が例外なく総数を返すことを検証する
    // Verify removal and disconnect of above-MaxStack records refund the full total without throwing
    public class GearChainRefundAboveMaxStackTest
    {
        private const int Remainder = 30;
        private static readonly Guid PoleCostItemGuid = Guid.Parse("00000000-0000-0000-1234-000000000003");

        [Test]
        public void RemovingPoleRefundsAboveMaxStackChainAsSplitStacks()
        {
            var world = new GearChainEditTestWorld(true);
            var total = ConnectLongChain(world);
            var response = world.SendRemoveBlock(PosA);

            Assert.IsTrue(response.Success, response.FailureReason.ToString());
            Assert.IsNull(ServerContext.WorldBlockDatastore.GetBlock(PosA));
            Assert.AreEqual(total, world.CountItem(world.ChainItemId));
            var maxStack = ItemStackLevelDataStore.Instance.GetMaxStack(world.ChainItemId);
            CollectionAssert.AreEqual(new[] { maxStack, maxStack, Remainder }, ChainStackCounts(world));
        }

        [TestCase(2, false)]
        [TestCase(3, true)]
        public void RemovalInsertionCheckCountsEverySplitStack(int freeSlots, bool expectSuccess)
        {
            var world = new GearChainEditTestWorld(true);
            var total = ConnectLongChain(world);
            // 分割後3スタックに対し空き枠の数だけを変える
            // Vary only the number of free slots against the three split stacks
            world.FillInventory();
            for (var slot = 0; slot < freeSlots; slot++) world.Inventory.SetItem(slot, ServerContext.ItemStackFactory.CreatEmpty());
            // ポール本体の建設コスト返却(Test3)は既存の端数スタックへ合流させ、空き枠をチェーン専用にする
            // Let the pole's own cost refund (Test3) merge into a partial stack so free slots count only chain stacks
            var poleCostItemId = MasterHolder.ItemMaster.GetItemId(PoleCostItemGuid);
            world.Inventory.SetItem(freeSlots, ServerContext.ItemStackFactory.Create(poleCostItemId, 1));
            var before = world.Inventory.InventoryItems.Select(stack => (stack.Id, stack.Count)).ToArray();
            var response = world.SendRemoveBlock(PosA);

            Assert.AreEqual(expectSuccess, response.Success);
            if (expectSuccess)
            {
                Assert.AreEqual(total, world.CountItem(world.ChainItemId));
                return;
            }
            Assert.AreEqual(RemoveBlockFailureReason.InventoryFull, response.FailureReason);
            Assert.IsNotNull(ServerContext.WorldBlockDatastore.GetBlock(PosA));
            Assert.IsTrue(Pole(PosA).ContainsChainConnection(Pole(PosB).BlockInstanceId));
            Assert.IsTrue(Pole(PosB).ContainsChainConnection(Pole(PosA).BlockInstanceId));
            CollectionAssert.AreEqual(before, world.Inventory.InventoryItems.Select(stack => (stack.Id, stack.Count)).ToArray());
        }

        [Test]
        public void DisconnectRefundsAboveMaxStackChainAsSplitStacks()
        {
            var world = new GearChainEditTestWorld(true);
            var total = ConnectLongChain(world);
            var response = world.Send(GearChainConnectionEditRequest.CreateDisconnectRequest(PosA, PosB));

            Assert.IsTrue(response.IsSuccess, response.Error);
            Assert.AreEqual(total, world.CountItem(world.ChainItemId));
            Assert.AreEqual(3, ChainStackCounts(world).Length);
        }

        private static int ConnectLongChain(GearChainEditTestWorld world)
        {
            // 長い線を想定し、MaxStackの2倍＋端数の支払い記録を両端へ置く
            // Record a long-line payment of twice MaxStack plus a remainder on both ends
            var total = ItemStackLevelDataStore.Instance.GetMaxStack(world.ChainItemId) * 2 + Remainder;
            var record = new ConnectionLineRecord(ChainToolGuid, new[] { new ConnectToolMaterialCost(world.ChainItemId, total) });
            Assert.IsTrue(Pole(PosA).TryAddChainConnection(Pole(PosB).BlockInstanceId, record));
            Assert.IsTrue(Pole(PosB).TryAddChainConnection(Pole(PosA).BlockInstanceId, record));
            return total;
        }

        private static int[] ChainStackCounts(GearChainEditTestWorld world)
        {
            return world.Inventory.InventoryItems.Where(stack => stack.Id == world.ChainItemId)
                .Select(stack => stack.Count).OrderByDescending(count => count).ToArray();
        }
    }
}
