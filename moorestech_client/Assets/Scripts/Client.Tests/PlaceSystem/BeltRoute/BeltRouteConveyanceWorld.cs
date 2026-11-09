using System;
using Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.Route;
using Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.Session.Branch;
using Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.World;
using Client.Tests.PlaceSystem.BeltRoute.World;
using Core.Master;
using Core.Update;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Extension;
using Game.Context;
using Game.World.Interface.DataStore;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;

namespace Client.Tests.PlaceSystem.BeltRoute
{
    // 搬送試験が共有する
    // ・テスト用サーバー組み立て
    // ・箱への写し取り
    // ・チェスト・tick 回し
    // Shared by the conveyance tests:
    // - test-server setup
    // - copying into the box
    // - chests and ticking
    public static class BeltRouteConveyanceWorld
    {
        private const int MaxTransportTicks = 600;
        public static readonly ItemId TestItemId = new(1);

        public static IWorldBlockDatastore BootWorld()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            return ServerContext.WorldBlockDatastore;
        }

        // 全ブロックを占有・入出力辺として箱へ写す（地形なし）
        // Copies every block into the box as occupancy and port edges (no terrain)
        public static BeltRouteGrid BuildGridFromWorld(IWorldBlockDatastore world)
        {
            var grid = new BeltRouteGrid(new Vector3Int(-8, 0, -8), new Vector3Int(24, 6, 24));
            foreach (var data in world.BlockMasterDictionary.Values)
            {
                var position = data.BlockPositionInfo;
                for (var x = position.MinPos.x; x <= position.MaxPos.x; x++)
                for (var y = position.MinPos.y; y <= position.MaxPos.y; y++)
                for (var z = position.MinPos.z; z <= position.MaxPos.z; z++)
                    grid.MarkBlockCell(new Vector3Int(x, y, z));
                BeltRouteBlockEdgeScanner.Write(data.Block.BlockMasterElement, data.Block.BlockId, position, grid);
            }
            return grid;
        }

        // 本番 Judge と同じく自身を除いた隣の辺を箱へ写す
        // Like the production Judge, copies the neighbours' edges into the box, excluding the cell itself
        public static BeltRouteNeighbourEdges BuildNeighbourEdges(IWorldBlockDatastore world, Vector3Int cell)
        {
            var edges = new BeltRouteNeighbourEdges();
            foreach (var data in world.BlockMasterDictionary.Values)
            {
                if (data.BlockPositionInfo.OriginalPos == cell) continue;
                BeltRouteBlockEdgeScanner.Write(data.Block.BlockMasterElement, data.Block.BlockId, data.BlockPositionInfo, edges);
            }
            return edges;
        }

        public static BlockId ResolveFamilyBlock(BlockId straightId, BeltRouteCellKind kind)
        {
            Assert.IsTrue(BeltConveyorPlaceFamilyUtil.TryGetFamily(straightId, out var family));
            return kind switch
            {
                BeltRouteCellKind.Up => family.UpBlockId.Value,
                BeltRouteCellKind.Down => family.DownBlockId.Value,
                _ => family.StraightBlockId,
            };
        }

        public static IOpenableBlockInventoryComponent PlaceChest(IWorldBlockDatastore world, Vector3Int position)
        {
            Assert.IsTrue(world.TryAddBlock(ForUnitTestModBlockId.ChestId, position, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var chest), $"chest placement failed at {position}");
            return chest.GetComponent<IOpenableBlockInventoryComponent>();
        }

        // 片方だけ待つときはsecondをnullにする
        // Pass null as second to wait for one chest only
        public static bool TickUntilDelivered(IOpenableBlockInventoryComponent first, int firstMin, IOpenableBlockInventoryComponent second, int secondMin)
        {
            for (var tick = 0; tick < MaxTransportTicks; tick++)
            {
                GameUpdater.UpdateOneTick();
                if (CountOf(first) < firstMin) continue;
                if (second == null || secondMin <= CountOf(second)) return true;
            }
            return false;
        }

        public static int CountOf(IOpenableBlockInventoryComponent inventory)
        {
            var count = 0;
            for (var i = 0; i < inventory.GetSlotSize(); i++)
                if (inventory.GetItem(i).Id == TestItemId) count += inventory.GetItem(i).Count;
            return count;
        }
    }
}
