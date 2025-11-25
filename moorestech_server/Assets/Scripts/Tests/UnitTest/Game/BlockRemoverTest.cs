using System;
using System.Collections.Generic;
using System.Linq;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Game.Context;
using Game.World.Interface.DataStore;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UniRx;
using UnityEngine;

namespace Tests.UnitTest.Game
{
    public class BlockRemoverTest
    {
        [Test]
        public void RemoveByInstancePublishesManualReason()
        {
            var (_, _) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var world = ServerContext.WorldBlockDatastore;
            var pos = new Vector3Int(8, 0, 0);
            world.TryAddBlock(ForUnitTestModBlockId.BlockId, pos, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var block);

            var reasons = new List<BlockRemoveReason>();
            using var subscription = ServerContext.WorldBlockUpdateEvent.OnBlockRemoveEvent.Subscribe(update => reasons.Add(update.RemoveReason));

            var removed = world.RemoveBlock(block.BlockInstanceId, BlockRemoveReason.ManualRemove);

            Assert.IsTrue(removed);
            Assert.IsFalse(world.Exists(pos));
            Assert.AreEqual(BlockRemoveReason.ManualRemove, reasons.Last());
        }
    }
}

