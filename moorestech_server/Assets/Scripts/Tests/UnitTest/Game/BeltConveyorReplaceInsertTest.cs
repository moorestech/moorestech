using System;
using Core.Master;
using Core.Update;
using Game.Block.Blocks.BeltConveyor;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Game.Context;
using Mooresmaster.Model.BlocksModule;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;

namespace Tests.UnitTest.Game
{
    /// <summary>
    /// リプレース設置の搬送品引き継ぎ用に進行率を維持したまま挿入できるか検証する
    /// Verify inserting an item preserving its progress rate for replace-placement handover
    /// </summary>
    public class BeltConveyorReplaceInsertTest
    {
        // 進行率0.5で挿入すると対応スロットに入りRemainingTicksが総tickの半分になる
        // Inserting at rate 0.5 fills the matched slot with half the total ticks remaining
        [Test]
        public void 進行率指定で挿入すると対応スロットとRemainingTicksが設定される()
        {
            var beltConveyorComponent = CreateBeltConveyor(out var beltParam);
            var totalTicks = GameUpdater.SecondsToTicks(beltParam.TimeOfItemEnterToExit);
            var slotCount = beltParam.BeltConveyorItemCount;

            // 進行率0.5でアイテムを挿入する
            // Insert an item at progress rate 0.5
            const double rate = 0.5;
            var itemId = new ItemId(2);
            var inserted = beltConveyorComponent.TryInsertItemWithRemainingRate(itemId, rate);
            Assert.IsTrue(inserted);

            // 期待スロットとRemainingTicksを算出して検証する
            // Compute and verify the expected slot and RemainingTicks
            var expectedIndex = Math.Clamp((int)Math.Ceiling(rate * slotCount) - 1, 0, slotCount - 1);
            var expectedRemaining = (uint)Math.Ceiling(totalTicks * rate);

            var slotItem = beltConveyorComponent.BeltConveyorItems[expectedIndex];
            Assert.IsNotNull(slotItem);
            Assert.AreEqual(itemId, slotItem.ItemId);
            Assert.AreEqual(expectedRemaining, slotItem.RemainingTicks);
        }

        // 全スロットが埋まると挿入はfalseを返す
        // Insertion returns false when all slots are occupied
        [Test]
        public void 満杯時は挿入に失敗してfalseを返す()
        {
            var beltConveyorComponent = CreateBeltConveyor(out var beltParam);
            var slotCount = beltParam.BeltConveyorItemCount;
            var itemId = new ItemId(2);

            // 出口側から順に全スロットを埋める
            // Fill every slot starting from the exit side
            for (var i = 0; i < slotCount; i++)
            {
                Assert.IsTrue(beltConveyorComponent.TryInsertItemWithRemainingRate(itemId, 0.0));
            }

            // 満杯状態ではfalseを返す
            // Returns false when full
            Assert.IsFalse(beltConveyorComponent.TryInsertItemWithRemainingRate(itemId, 0.0));
        }

        #region Internal

        private static VanillaBeltConveyorComponent CreateBeltConveyor(out BeltConveyorBlockParam beltParam)
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            beltParam = MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.BeltConveyorId).BlockParam as BeltConveyorBlockParam;
            var beltConveyor = ServerContext.BlockFactory.Create(ForUnitTestModBlockId.BeltConveyorId, new BlockInstanceId(int.MaxValue), new BlockPositionInfo(Vector3Int.one, BlockDirection.North, Vector3Int.one));
            return beltConveyor.GetComponent<VanillaBeltConveyorComponent>();
        }

        #endregion
    }
}
