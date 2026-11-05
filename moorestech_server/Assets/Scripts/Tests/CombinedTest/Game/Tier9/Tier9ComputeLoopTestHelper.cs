using System;
using Core.Item.Interface;
using Core.Master;
using Core.Update;
using Game.Block.Blocks.Machine;
using Game.Block.Blocks.Machine.Inventory;
using Game.Block.Blocks.RocketLaunchPad;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Extension;
using Game.Block.Interface.State;
using Game.Context;
using Game.PlayerInventory.Interface;
using NUnit.Framework;
using Tests.Module.TestMod;
using Tests.Util.Rocket;
using static Tests.CombinedTest.Core.Machine.Upgrade.LevelInheritanceTestHelper;

namespace Tests.CombinedTest.Game.Tier9
{
    // G4 の連鎖の加工・打ち上げ・移し替え
    // Crafting, launching and hand-over steps of the G4 chain
    public static class Tier9ComputeLoopTestHelper
    {
        private static int PhaseTimeoutTicks => (int)GameUpdater.SecondsToTicks(10);

        public static void RunOneCraft(VanillaMachineProcessorComponent processor, Guid recipeGuid)
        {
            AdvanceTicksWithFullPower(TicksToComplete(Recipe(recipeGuid)), processor);
        }

        // 出力口から指定数を抜き、抜いた分のスタックを返す
        // Take the given count out of the output slots and return it as a stack
        public static IItemStack TakeOutput(VanillaMachineBlockInventoryComponent inventory, ItemId itemId, int count)
        {
            for (var slot = InputSlotCount; slot < InputSlotCount + OutputSlotCount; slot++)
            {
                var stack = inventory.GetItem(slot);
                if (stack.Id != itemId || stack.Count < count) continue;
                inventory.SetItem(slot, stack.SubItem(count));
                return Create(itemId, count);
            }
            Assert.Fail($"出力口に {itemId} が {count} 個ありません");
            return null;
        }

        // 初打ち上げは本番の口から合図を送る
        // The first launch sends commands through the production entry
        public static void LaunchFirst(IBlock pad)
        {
            var processor = pad.GetComponent<RocketLaunchPadProcessorComponent>();
            Assert.IsTrue(RocketLaunchPadTestUtil.RunUntilPhase(pad, RocketLaunchPadPhase.AwaitingBuildCommand, PhaseTimeoutTicks));
            Assert.AreEqual(RocketLaunchPadCommandResult.Accepted, processor.RequestBuild());
            Assert.IsTrue(RocketLaunchPadTestUtil.RunUntilPhase(pad, RocketLaunchPadPhase.AwaitingLaunchCommand, PhaseTimeoutTicks));
            Assert.AreEqual(RocketLaunchPadCommandResult.Accepted, processor.RequestLaunch());
            Assert.IsTrue(RocketLaunchPadTestUtil.RunUntilPhase(pad, RocketLaunchPadPhase.Launching, PhaseTimeoutTicks));
        }

        // 2便目以降は前の便を待って積む
        // Later launches wait for the previous flight, then load
        public static void LaunchNext(IBlock pad, ItemId payloadItemId, int payloadCount)
        {
            Assert.IsTrue(RocketLaunchPadTestUtil.RunUntilPhase(pad, RocketLaunchPadPhase.Idle, PhaseTimeoutTicks));
            RocketLaunchPadTestUtil.FillOneLaunch(pad, payloadItemId, payloadCount);
            Assert.IsTrue(RocketLaunchPadTestUtil.RunUntilPhase(pad, RocketLaunchPadPhase.Launching, PhaseTimeoutTicks));
        }

        // 出力口の計算データをプレイヤーへ移す
        // Move the pad's compute data outputs to the player
        public static int MovePadOutputsToPlayer(IBlock pad, int playerId)
        {
            var padInventory = pad.GetComponent<IOpenableBlockInventoryComponent>();
            var playerInventory = ServerContext.GetService<IPlayerInventoryDataStore>().GetInventoryData(playerId).MainOpenableInventory;
            var moved = 0;
            foreach (var slot in RocketLaunchPadTestUtil.GetLayout(pad).OutputSlots)
            {
                var stack = padInventory.GetItem(slot);
                if (stack.Count == 0) continue;
                Assert.AreEqual(ForUnitTestItemId.ComputeDataId, stack.Id, $"発射台の出力口 {slot} に計算データ以外が出ています");
                moved += stack.Count;
                Assert.AreEqual(0, playerInventory.InsertItem(stack).Count);
                padInventory.SetItem(slot, ServerContext.ItemStackFactory.CreatEmpty());
            }
            return moved;
        }

        public static int CountPlayerItem(int playerId, ItemId itemId)
        {
            var playerInventory = ServerContext.GetService<IPlayerInventoryDataStore>().GetInventoryData(playerId).MainOpenableInventory;
            return CountItem(playerInventory, 0, playerInventory.GetSlotSize(), itemId);
        }
    }
}
