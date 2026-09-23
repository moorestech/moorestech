using System;
using System.Linq;
using Core.Master;
using Core.Update;
using Game.BeltSegment;
using Game.Block.Blocks.BeltConveyor;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Game.Context;
using Game.SaveLoad.Json;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;
namespace Tests.CombinedTest.Game.BeltSegmentWorld
{
    internal sealed class BeltWorldFixture
    {
        internal readonly ServiceProvider Services;
        internal readonly BeltWorldDatastore Belts;
        internal BeltWorldFixture()
        {
            (_, Services) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            GameUpdater.RestoreCurrentTick(0);
            Belts = Services.GetRequiredService<BeltWorldDatastore>();
        }
        internal IBlock Add(BlockId id, Vector3Int p, BlockDirection d)
        {
            Assert.IsTrue(ServerContext.WorldBlockDatastore.TryAddBlock(id, p, d, Array.Empty<BlockCreateParam>(), out var block));
            return block;
        }
        internal SegmentBeltComponent Belt(Vector3Int p, BlockDirection d)
            => Add(ForUnitTestModBlockId.BeltConveyorId, p, d).GetComponent<SegmentBeltComponent>();
        internal void Tick(int count) { for (int i = 0; i < count; i++) GameUpdater.Update(); }
        internal void Seed(SegmentBeltComponent belt, int itemId) => belt.SetItem(0, ServerContext.ItemStackFactory.Create(new ItemId(itemId), 1));
        internal string Save() => Services.GetRequiredService<AssembleSaveJsonText>().AssembleSaveJson();
        internal BeltWorldSnapshot Snapshot() => Belts.CaptureSnapshot();
        internal static int Count(BeltWorldSnapshot snapshot)
            => snapshot.Simulation.Segments.Sum(s => s.Items.Length + (s.BufferedItem.HasValue ? 1 : 0));
        internal SegmentBeltComponent[] Loop(bool reverse)
        {
            var p = new[] { Vector3Int.zero, Vector3Int.forward, Vector3Int.forward + Vector3Int.right, Vector3Int.right };
            var d = new[] { BlockDirection.North, BlockDirection.East, BlockDirection.South, BlockDirection.West };
            var belts = new SegmentBeltComponent[4];
            for (int n = 0; n < 4; n++) { int i = reverse ? 3 - n : n; belts[i] = Belt(p[i], d[i]); }
            return belts;
        }
    }
}
