using Core.Master;
using Game.Block.Blocks.Machine.Inventory.Input;
using Game.Block.Interface;
using Game.Block.Interface.MachineUpgrade;
using Game.Context;
using NUnit.Framework;
using Tests.Module.TestMod;
using UnityEngine;
using static Tests.CombinedTest.Core.Machine.Upgrade.InputLevelStall.MachineInputLevelStallTestUtil;
using static Tests.CombinedTest.Core.Machine.Upgrade.LevelInheritanceTestHelper;
using static Tests.Module.TestMod.ForUnitTestLevelInheritanceIds;
using static Tests.Util.BeltWorldTestUtil;

namespace Tests.CombinedTest.Core.Machine.Upgrade.InputLevelStall
{
    /// <summary>
    ///     ベルトの先頭にレベル違い・製作上限超えの継承元が来ると止まった理由が立ち、原因が消えると消えることを、実際のtick経路で検証する
    ///     Through the real tick path, verifies the stall appears when a different-level or above-cap source heads the belt and clears once the cause is gone
    /// </summary>
    public class MachineInputLevelStallBeltTest
    {
        private static readonly Vector3Int BeltPos = new(0, 0, -1);
        private static readonly Vector3Int ChestPos = new(0, 0, -2);

        // チェストからベルト1マス（約60tick）を渡って機械の入口で待つまでに余裕を持たせたtick数
        // Ticks with margin for an item to leave the chest, cross one belt cell (about 60 ticks) and wait at the machine inlet
        private const int DeliveryTicks = 300;

        [Test]
        public void AboveCraftCapOnEmptySlotStallsUntilCapIsRaisedTest()
        {
            var (block, inventory) = SetUpMachine(CappedInheritRecipeGuid);
            var chest = PlaceFeedLine();

            // 空のスロットへ製作上限(Lv1)を超える Lv2 が1個届くと、上限超えで止まる
            // A single Lv2 above the craft cap (Lv1) arriving at an empty slot stalls as above-cap
            FeedOne(chest, Source(2));
            AssertStalled(block, MachineInputLevelRefusalReason.AboveCraftCap, Source(2));

            // 上限を2へ上げると、待っていた Lv2 が入って理由が消える
            // Raising the cap to 2 lets the waiting Lv2 in and the stall clears
            ServerContext.GetService<ILevelInheritanceCraftCapUnlocker>().UnlockCraftCap(CappedTargetBaseGuid, 2);
            RunTicks(3);
            Assert.AreEqual(1, CountItem(inventory, 0, InputSlotCount, Source(2)));
            AssertNotStalled(block);
        }

        [Test]
        public void MixedLevelClearsWithinTwoTicksWhenBeltIsRemovedTest()
        {
            var (block, inventory) = SetUpMachine(InheritRecipeGuid);
            var chest = PlaceFeedLine();
            inventory.SetItem(0, Create(Source(1), 1));

            // Lv1 が1個入ったスロットへ Lv2 が1個だけ届くと、レベル違いで止まる
            // A single Lv2 arriving at a slot holding one Lv1 stalls as mixed level
            FeedOne(chest, Source(2));
            AssertStalled(block, MachineInputLevelRefusalReason.MixedLevel, Source(2));

            // ベルトを撤去すると誰も渡し直さないので、2tick以内に消える（止まったまま残らない）
            // With the belt removed nobody re-offers, so it clears within two ticks instead of staying forever
            Assert.IsTrue(ServerContext.WorldBlockDatastore.RemoveBlock(BeltPos, BlockRemoveReason.ManualRemove));
            RunTicks(2);
            AssertNotStalled(block);
        }

        [Test]
        public void TakingOutLowerLevelLetsWaitingItemInTest()
        {
            var (block, inventory) = SetUpMachine(InheritRecipeGuid);
            var chest = PlaceFeedLine();
            inventory.SetItem(0, Create(Source(1), 1));
            FeedOne(chest, Source(2));
            AssertStalled(block, MachineInputLevelRefusalReason.MixedLevel, Source(2));

            // プレイヤーが Lv1 を取り出すと、待っていた Lv2 が入って理由が消える
            // When the player takes the Lv1 out, the waiting Lv2 goes in and the stall clears
            inventory.SetItem(0, ServerContext.ItemStackFactory.CreatEmpty());
            RunTicks(3);
            Assert.AreEqual(1, CountItem(inventory, 0, InputSlotCount, Source(2)));
            AssertNotStalled(block);
        }

        // 機械の手前にベルト1マスとチェストを北向きに並べ、チェストを返す
        // Line one belt cell and a chest facing north in front of the machine and return the chest
        private static IBlock PlaceFeedLine()
        {
            Place(ForUnitTestModBlockId.BeltConveyorId, BeltPos, BlockDirection.North);
            return Place(ForUnitTestModBlockId.ChestId, ChestPos, BlockDirection.North);
        }

        // チェストへ1個入れ、ベルトの先頭が機械の入口で待つまで進める
        // Put one item in the chest and advance until the belt head waits at the machine inlet
        private static void FeedOne(IBlock chest, ItemId itemId)
        {
            Inventory(chest).SetItem(0, Create(itemId, 1));
            RunTicks(DeliveryTicks);
        }
    }
}
