using System;
using System.Linq;
using Core.Master;
using Game.Blueprint;
using Game.Context;
using Game.World.Interface.DataStore;
using NUnit.Framework;
using Server.Protocol.PacketResponse;
using Tests.Module.TestMod;
using UnityEngine;

namespace Tests.CombinedTest.Server.PacketTest
{
    public class BlueprintPasteProtocolTest
    {
        [Test]
        public void 素材が一つ足りなければBP全体を拒否するTest()
        {
            using var context = new BlueprintPasteProtocolTestContext(true, false);
            var blueprint = context.Create(ForUnitTestModBlockId.BlockId, ForUnitTestModBlockId.BlockId);
            context.Register(blueprint);
            context.Supply(blueprint, 1);

            // 素材1個不足で原子性を検証
            // Verify atomicity with one missing material unit.
            var stack = context.Inventory.InventoryItems.First(item => 0 < item.Count);
            context.Inventory.SetItem(Array.IndexOf(context.Inventory.InventoryItems.ToArray(), stack),
                ServerContext.ItemStackFactory.Create(stack.Id, stack.Count - 1));
            var held = context.Inventory.InventoryItems.Sum(item => item.Count);
            context.Paste(blueprint, 0, BlueprintPasteProtocolTestContext.Origin);

            Assert.AreEqual(0, ServerContext.WorldBlockDatastore.BlockMasterDictionary.Count);
            Assert.AreEqual(held, context.Inventory.InventoryItems.Sum(item => item.Count));
            context.AssertDenied(BlueprintFailureReason.PasteCostShortage, 1);
        }

        [Test]
        public void 列は賄える個数までBP単位で置かれるTest()
        {
            using var context = new BlueprintPasteProtocolTestContext(true, false);
            var blueprint = context.Create(ForUnitTestModBlockId.BlockId, ForUnitTestModBlockId.BlockId);
            context.Register(blueprint);
            context.Supply(blueprint, 2);
            var origins = Enumerable.Range(0, 3).Select(i => new Vector3Int(10 + i * 20, 0, 10)).ToArray();

            context.Paste(blueprint, 0, origins);

            // 2BPを配置し3個目は全体拒否
            // Place two copies and reject the entire third copy.
            Assert.AreEqual(4, ServerContext.WorldBlockDatastore.BlockMasterDictionary.Count);
            foreach (var origin in origins.Take(2))
            {
                Assert.IsTrue(ServerContext.WorldBlockDatastore.Exists(origin));
                Assert.IsTrue(ServerContext.WorldBlockDatastore.Exists(origin + new Vector3Int(3, 0, 0)));
            }
            Assert.IsFalse(ServerContext.WorldBlockDatastore.Exists(origins[2]));
            context.AssertDenied(BlueprintFailureReason.PasteCostShortage, 1);
        }

        [Test]
        public void 全ブロック重なりなら既存ブロックと素材を保持するTest()
        {
            using var context = new BlueprintPasteProtocolTestContext(true, false);
            var blueprint = context.Create(ForUnitTestModBlockId.BlockId);
            context.Register(blueprint);
            context.Supply(blueprint, 1);
            var existing = BlueprintPasteProtocolTestContext.Place(ForUnitTestModBlockId.BlockId, BlueprintPasteProtocolTestContext.Origin);
            var held = context.Inventory.InventoryItems.Sum(item => item.Count);

            context.Paste(blueprint, 0, BlueprintPasteProtocolTestContext.Origin);

            Assert.AreSame(existing, ServerContext.WorldBlockDatastore.GetBlock(BlueprintPasteProtocolTestContext.Origin));
            Assert.AreEqual(held, context.Inventory.InventoryItems.Sum(item => item.Count));
        }
    }
}
