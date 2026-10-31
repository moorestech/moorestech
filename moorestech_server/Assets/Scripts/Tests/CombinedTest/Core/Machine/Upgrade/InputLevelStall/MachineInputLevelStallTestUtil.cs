using System;
using Core.Master;
using Core.Update;
using Game.Block.Blocks.Machine.Inventory;
using Game.Block.Blocks.Machine.Inventory.Input;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using Tests.Util;
using UnityEngine;
using static Tests.CombinedTest.Core.Machine.Upgrade.LevelInheritanceTestHelper;

namespace Tests.CombinedTest.Core.Machine.Upgrade.InputLevelStall
{
    /// <summary>
    ///     止まった理由のテストの共通処理（継承機械の設置・tick送り・止まった理由の確認）
    ///     Shared helpers for the stall tests (placing the inheritance machine, advancing ticks, checking the stall)
    /// </summary>
    internal static class MachineInputLevelStallTestUtil
    {
        public static readonly Vector3Int MachinePos = Vector3Int.zero;

        // サーバー起動後に継承機械とレシピを設定
        // Start the server, then set up the inheritance machine and recipe
        public static (IBlock block, VanillaMachineBlockInventoryComponent inventory) SetUpMachine(Guid recipeGuid)
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var (block, inventory, _) = PlaceMachine(MachinePos);
            MachineRecipeSelectTestUtil.SelectRecipe(block, LevelInheritanceTestHelper.Recipe(recipeGuid));
            return (block, inventory);
        }

        public static void RunTicks(int ticks)
        {
            for (var i = 0; i < ticks; i++) GameUpdater.UpdateOneTick();
        }

        public static void AssertStalled(IBlock block, MachineInputLevelRefusalReason reason, ItemId waitingItemId)
        {
            Assert.IsTrue(block.GetComponent<MachineInputLevelStallComponent>().TryGetStall(out var stall), "the machine should report a level stall");
            Assert.AreEqual(reason, stall.Reason);
            Assert.AreEqual(waitingItemId, stall.WaitingItemId);
        }

        public static void AssertNotStalled(IBlock block)
        {
            Assert.IsFalse(block.GetComponent<MachineInputLevelStallComponent>().TryGetStall(out _), "the machine should not report a level stall");
        }
    }
}
