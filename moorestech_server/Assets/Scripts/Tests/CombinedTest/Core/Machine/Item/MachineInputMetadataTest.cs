using System;
using System.Collections.Generic;
using Core.Item.Interface;
using Core.Master;
using Game.Block.Blocks.Machine.Inventory;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Extension;
using Game.Context;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using Tests.UnitTest.Core.Other;
using Tests.Util;
using UnityEngine;

namespace Tests.CombinedTest.Core.Machine
{
    public class MachineInputMetadataTest
    {
        [TestCase(false, false, 1)]
        [TestCase(false, true, 1)]
        [TestCase(true, false, 1)]
        [TestCase(true, true, 1)]
        [TestCase(true, false, 10)]
        [TestCase(true, true, 10)]
        public void FilledRequirementPreservesMetadataAcceptance(bool automatic, bool matching, int incomingCount)
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var recipe = MasterHolder.MachineRecipesMaster.GetRecipeElement(new Guid("bd3d4d7d-9c3b-4ae1-875b-950327eedd9d"));
            var blockId = MasterHolder.BlockMaster.GetBlockId(recipe.BlockGuid);
            ServerContext.WorldBlockDatastore.TryAddBlock(blockId, Vector3Int.one, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var block);
            MachineRecipeSelectTestUtil.SelectRecipe(block, recipe);
            var inventory = block.GetComponent<VanillaMachineBlockInventoryComponent>();

            // 必要数を満たし、第二パスの受入判定を検証する
            // Fill the requirement to exercise second-pass acceptance
            var factory = ServerContext.ItemStackFactory;
            var itemId = MasterHolder.ItemMaster.GetItemId(recipe.InputItems[0].ItemGuid);
            var metadata = new Dictionary<string, ItemStackMetaData> { { "test1", new TestMeta1() } };
            var stored = matching ? factory.Create(itemId, 3, metadata) : factory.Create(itemId, 3);
            inventory.SetItem(0, stored);
            var incoming = factory.Create(itemId, incomingCount, metadata);
            var batch = new List<IItemStack> { incoming };
            var accepted = matching ? Math.Min(incomingCount, automatic ? 6 : incomingCount) : 0;

            // 可否判定は書き込まず、実挿入と一致する
            // Availability checks do not write and agree with actual insertion
            var canInsert = automatic ? ((IBlockInventory)inventory).InsertionCheck(batch) : inventory.InsertionCheck(batch);
            Assert.AreEqual(accepted == incomingCount, canInsert);
            Assert.AreSame(stored, inventory.GetItem(0));
            var remainder = automatic ? ((IBlockInventory)inventory).InsertItem(incoming, InsertItemContext.Empty) : inventory.InsertItem(incoming);
            Assert.AreEqual(3 + accepted, inventory.GetItem(0).Count);
            Assert.AreEqual(incomingCount - accepted, remainder.Count);
            if (0 < remainder.Count) Assert.IsInstanceOf<TestMeta1>(remainder.GetMeta("test1"));
        }
    }
}
