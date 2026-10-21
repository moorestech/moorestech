using System;
using System.Linq;
using Core.BeltTransport;
using Core.Master;
using Game.Block.Blocks.BeltConveyor.Transport;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Context;
using UnityEngine;

namespace Tests.Util
{
    // 実際のtick経路(MasterTickUpdater)でベルト搬送を確かめる結合テスト用の補助
    // Helpers for combined tests that check belt transport through the real tick path (MasterTickUpdater)
    public static class BeltWorldTestUtil
    {
        public static IBlock Place(BlockId blockId, Vector3Int position, BlockDirection direction)
        {
            ServerContext.WorldBlockDatastore.TryAddBlock(blockId, position, direction, Array.Empty<BlockCreateParam>(), out var block);
            return block;
        }

        public static IOpenableBlockInventoryComponent Inventory(IBlock block)
        {
            return block.ComponentManager.GetComponent<IOpenableBlockInventoryComponent>();
        }

        // ベルコンの搬入口。スロットを持たないので開けるインベントリではない
        // The belt inlet; it has no slots and is not an openable inventory
        public static IBlockInventory Inlet(IBlock belt)
        {
            return belt.ComponentManager.GetComponent<IBlockInventory>();
        }

        public static int CountOf(IOpenableBlockInventoryComponent inventory, ItemId itemId)
        {
            var count = 0;
            for (var i = 0; i < inventory.GetSlotSize(); i++)
                if (inventory.GetItem(i).Id == itemId) count += inventory.GetItem(i).Count;
            return count;
        }

        // 現在のワールド全体の搬送の組。tick外の設置・撤去は次のtickを進める前に作り直される
        // The current world-wide transport assembly; placements and removals outside a tick are rebuilt before the next tick advances
        public static BeltTransportAssembly Assembly()
        {
            return ServerContext.GetService<BeltTransportDatastore>().Assembly;
        }

        // tick外の設置を反映してから、機械の押し込みを受け付ける区間(block更新中)に入る。tickの外から搬入口へ押し込むテストが使う
        // Apply placements made outside a tick, then enter the window that accepts machine pushes (block updates); used by tests that push into an inlet from outside a tick
        public static void EnterMachinePushWindow()
        {
            var mutation = ServerContext.GetService<IBeltTransportMutation>();
            mutation.RebuildIfDirty();
            mutation.BeginMachinePushWindow();
        }

        // 指定マスを含むsegmentを返す
        // The segment containing the given cell
        public static BeltConveyorSegment SegmentAt(Vector3Int position)
        {
            var assembly = Assembly();
            var index = assembly.Layouts.Single(layout => layout.Cells.Any(cell => cell.Position == position)).Index;
            return assembly.Segments[index];
        }

        // 指定マスを含むsegmentのアイテムを出口に近い順で返す
        // Items of the segment containing the given cell, in exit order
        public static BeltItemState[] ItemsOnSegmentAt(Vector3Int position)
        {
            return SegmentAt(position).CaptureItems();
        }
    }
}
