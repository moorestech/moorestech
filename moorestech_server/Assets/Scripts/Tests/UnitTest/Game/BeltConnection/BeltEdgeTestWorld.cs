using System;
using System.Collections.Generic;
using System.Linq;
using Core.Master;
using Game.Block.Blocks.BeltConveyor.Connection;
using Game.Block.Component;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Component.ConnectJudge;
using Game.Context;
using Game.World;
using Game.World.Interface.DataStore;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using Tests.UnitTest.Game.BeltConnection.Fixtures;
using UnityEngine;

namespace Tests.UnitTest.Game.BeltConnection
{
    internal sealed class BeltEdgeTestWorld
    {
        internal static readonly string[] Slots = { "UL", "UR", "LL", "LR" };
        internal readonly IWorldBlockDatastore World;
        private readonly WorldBlockUpdateEvent _events;
        private readonly Dictionary<string, IBlock> _blocks = new();
        private readonly bool _gear;
        private readonly BlockDirection _rotation;
        internal readonly BeltEdge Edge;

        internal BeltEdgeTestWorld(bool gear, BlockDirection rotation)
        {
            var (_, services) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            BeltTestMaster.Load(services.GetRequiredService<MasterJsonFileContainer>());
            World = ServerContext.WorldBlockDatastore;
            _events = (WorldBlockUpdateEvent)ServerContext.WorldBlockUpdateEvent;
            _gear = gear;
            _rotation = rotation;
            Edge = new BeltEdge(Position("UL"), rotation.ConvertLocalCell(Vector3Int.forward), 0);
        }

        internal Vector3Int Position(string slot)
        {
            var index = Array.IndexOf(Slots, slot);
            // セル中心を回してから整数セルへ戻す
            // Rotate cell centers before converting back to integer cells
            var center = new Vector3(0.5f, 0, index % 2 == 0 ? -0.5f : 0.5f);
            var rotated = _rotation.GetRotation() * center;
            return new Vector3Int(Mathf.FloorToInt(rotated.x + 0.0001f), 2 <= index ? -1 : 0, Mathf.FloorToInt(rotated.z + 0.0001f));
        }

        internal IBlock Place(string slot, int code)
        {
            // 承認済みfixtureの占有状態だけを配置する
            // Place only occupied states defined by the approved fixture
            if (code < 1 || 6 < code) throw new ArgumentOutOfRangeException(nameof(code), code, "Occupied belt state codes must be 1 through 6.");
            var kind = (code - 1) % 3;
            BlockId id = _gear ? kind switch
            {
                0 => ForUnitTestModBlockId.GearBeltConveyor,
                1 => ForUnitTestModBlockId.TestGearBeltConveyorUp,
                _ => ForUnitTestModBlockId.TestGearBeltConveyorDown
            } : kind switch
            {
                0 => ForUnitTestModBlockId.BeltConveyorId,
                1 => BeltTestMaster.Up,
                _ => BeltTestMaster.Down
            };
            var direction = code <= 3 ? _rotation : _rotation.HorizonRotation().HorizonRotation();
            Assert.IsTrue(World.TryAddBlock(id, Position(slot), direction, Array.Empty<BlockCreateParam>(), out var block));
            _blocks.Add(slot, block);
            return block;
        }

        internal void Remove(string slot)
        {
            if (!_blocks.Remove(slot)) return;
            Assert.IsTrue(World.RemoveBlock(Position(slot), BlockRemoveReason.ManualRemove));
        }

        // 捕捉済みのworldイベントで、同じ配置の再通知を検証する
        // Reannounce the same placement using the event source captured with this world
        internal void ReannouncePlacement(string slot)
        {
            var position = Position(slot);
            _events.OnBlockPlaceEventInvoke(position, World.GetOriginPosBlock(position));
        }

        internal void Clear()
        {
            foreach (var slot in Slots) Remove(slot);
        }

        internal void AssertEdges(IEnumerable<string> expected, string label)
        {
            // 辞書の実接続とresolverの結果を独立して照合する
            // Check actual dictionary connections and resolver results independently
            var actual = new List<string>();
            foreach (var source in _blocks)
            {
                BeltInventoryConnectionData.TryGet(source.Value, out var context);
                if (!context.Edges.Contains(Edge)) continue;
                foreach (var target in Connector(source.Value).ConnectedTargets.Values)
                {
                    if (!BeltInventoryConnectionData.TryGet(target.TargetBlock, out var targetContext) || !targetContext.Edges.Contains(Edge)) continue;
                    actual.Add(source.Key + ">" + _blocks.Single(p => ReferenceEquals(p.Value, target.TargetBlock)).Key);
                }
            }
            CollectionAssert.AreEquivalent(expected, actual, label);
            var resolved = new List<BeltEdgeConnection>();
            BeltEdgeConnectionResolver.Resolve(World, Edge, null, resolved);
            Assert.AreEqual(actual.Count, resolved.Count, label + " resolver count");
        }

        internal static BlockConnectorComponent<IBlockInventory, BeltInventoryConnectionContext> Connector(IBlock block) =>
            block.ComponentManager.GetComponent<BlockConnectorComponent<IBlockInventory, BeltInventoryConnectionContext>>();
    }
}
