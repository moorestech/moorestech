using Client.Game.InGame.Block;
using Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.Route;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.World
{
    /// <summary>
    ///     測定済みの地形に既存ブロックの占有・坂の真上・入出力辺を重ねて探索箱を作る。ブロックが変わるたびに作り直す
    ///     Builds the search box by layering existing blocks' occupancy, slope heads and port edges over the measured terrain; rebuilt whenever blocks change
    /// </summary>
    public sealed class BeltRouteWorldScanner
    {
        // 箱の外にある大きなブロックが箱の中へ張り出す分と、箱の縁へ向く辺を拾うための余白
        // Margin so blocks outside the box that reach into it, and edges facing the box rim, are still picked up
        private const int BlockScanMargin = 16;

        private readonly BlockGameObjectDataStore _blocks;

        public BeltRouteWorldScanner(BlockGameObjectDataStore blocks)
        {
            _blocks = blocks;
        }

        public BeltRouteGrid Build(BeltRouteTerrainProfile terrain)
        {
            var grid = terrain.CreateGrid();
            foreach (var block in _blocks.BlockGameObjectDictionary.Values)
            {
                if (!IsNearBox(grid, block.BlockPosInfo.OriginalPos)) continue;
                MarkFootprint(grid, block);
                BeltRouteBlockEdgeScanner.Write(block, grid);
            }
            return grid;
        }

        // 置かれた・撤去されたブロックが箱に関わるときだけ作り直す
        // Rebuild only when a placed or removed block can affect the box
        public static bool IsNearBox(BeltRouteGrid grid, Vector3Int blockOrigin)
        {
            return grid.Min.x - BlockScanMargin <= blockOrigin.x && blockOrigin.x < grid.Min.x + grid.Size.x + BlockScanMargin &&
                   grid.Min.z - BlockScanMargin <= blockOrigin.z && blockOrigin.z < grid.Min.z + grid.Size.z + BlockScanMargin;
        }

        private static void MarkFootprint(BeltRouteGrid grid, BlockGameObject block)
        {
            var min = block.BlockPosInfo.MinPos;
            var max = block.BlockPosInfo.MaxPos;
            for (var x = min.x; x <= max.x; x++)
            for (var y = min.y; y <= max.y; y++)
            for (var z = min.z; z <= max.z; z++)
                grid.MarkBlockCell(new Vector3Int(x, y, z));
        }
    }
}
