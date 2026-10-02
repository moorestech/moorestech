using System;
using System.Collections.Generic;
using System.Linq;
using Core.BeltTransport;
using Core.Master;
using Game.Block.Blocks.BeltConveyor.Topology;
using Game.Block.Interface;
using Game.World.Interface.DataStore;
using NUnit.Framework;
using UnityEngine;

namespace Tests.UnitTest.Game.BeltConnection.Topology
{
    internal static class BeltTopologyTestUtil
    {
        internal static IBlock Place(IWorldBlockDatastore world, BlockId id, Vector3Int position, BlockDirection direction)
        {
            Assert.IsTrue(world.TryAddBlock(id, position, direction, Array.Empty<BlockCreateParam>(), out var block), $"place {id} at {position}");
            return block;
        }

        internal static BeltTopologyCell Cell(List<BeltTopologyCell> cells, Vector3Int position)
        {
            return cells.Single(cell => cell.Position == position);
        }

        internal static void AssertConnection(BeltTopologyConnection connection, BeltDirection direction, BeltEntryDirection entryDirection,
            BeltTopologyPartnerKind partnerKind, Vector3Int partnerCell)
        {
            Assert.AreEqual(direction, connection.Direction, "direction");
            Assert.AreEqual(entryDirection, connection.EntryDirection, "entry direction");
            Assert.AreEqual(partnerKind, connection.PartnerKind, "partner kind");
            Assert.AreEqual(partnerCell, connection.PartnerCell, "partner cell");
        }

        // インスタンスIDを除いた比較用の文字列。別ワールド同士の決定性比較に使う
        // Comparison string without instance ids, used to compare determinism across separate worlds
        internal static List<string> Signature(List<BeltTopologyCell> cells)
        {
            return cells.Select(cell =>
                $"{cell.Position} fwd={cell.Forward} speed={cell.BeltSpeedPerTick} splitter={cell.IsSplitter} " +
                $"in=[{string.Join(",", cell.Inputs.Select(Describe))}] out=[{string.Join(",", cell.Outputs.Select(Describe))}]").ToList();

            string Describe(BeltTopologyConnection connection) =>
                $"{connection.Direction}/{connection.EntryDirection}/{connection.PartnerKind}/{connection.PartnerCell}";
        }
    }
}
