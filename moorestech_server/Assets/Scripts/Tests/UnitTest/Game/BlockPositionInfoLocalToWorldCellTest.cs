using System.Collections.Generic;
using Game.Block.Interface;
using NUnit.Framework;
using UnityEngine;

namespace Tests.UnitTest.Game
{
    /// <summary>
    ///     ブロックローカルセルの回転換算を検証する（採掘機のドリル位置とコネクターのoffsetが共有する規約）
    ///     Verifies the block-local cell conversion shared by miner drill positions and connector offsets
    /// </summary>
    public class BlockPositionInfoLocalToWorldCellTest
    {
        private static readonly Vector3Int BlockSize = new(2, 1, 3);
        private static readonly Vector3Int OriginPos = new(10, 4, -7);

        /// <summary>
        ///     ブロックローカルの全セルを換算するとブロックの占有範囲と一致する。基準座標や回転を取り違えると範囲外へ飛ぶ
        ///     Converting every block-local cell must reproduce the occupied range; a wrong base origin or rotation lands outside it
        /// </summary>
        [Test]
        public void 全ローカルセルの換算結果がブロックの占有範囲と一致する(
            [Values(BlockDirection.North, BlockDirection.East, BlockDirection.South, BlockDirection.West)] BlockDirection direction)
        {
            var blockPositionInfo = new BlockPositionInfo(OriginPos, direction, BlockSize);

            var occupiedCells = new HashSet<Vector3Int>();
            for (var x = blockPositionInfo.MinPos.x; x <= blockPositionInfo.MaxPos.x; x++)
            for (var y = blockPositionInfo.MinPos.y; y <= blockPositionInfo.MaxPos.y; y++)
            for (var z = blockPositionInfo.MinPos.z; z <= blockPositionInfo.MaxPos.z; z++)
                occupiedCells.Add(new Vector3Int(x, y, z));

            var drillCells = new HashSet<Vector3Int>();
            for (var x = 0; x < BlockSize.x; x++)
            for (var y = 0; y < BlockSize.y; y++)
            for (var z = 0; z < BlockSize.z; z++)
                drillCells.Add(blockPositionInfo.ConvertBlockLocalToWorldCell(new Vector3Int(x, y, z)));

            CollectionAssert.AreEquivalent(occupiedCells, drillCells, $"drill cells do not cover the block footprint for {direction}");
        }

        /// <summary>
        ///     回転で実際にワールドセルが変わること。回転を落として原点をそのまま返す実装をここで落とす
        ///     Rotation must actually move the world cell; an implementation that drops the rotation fails here
        /// </summary>
        [Test]
        public void 同じローカル位置でも向きが変わればワールドセルが変わる()
        {
            var blockLocalCell = new Vector3Int(1, 0, 2);

            var north = new BlockPositionInfo(OriginPos, BlockDirection.North, BlockSize).ConvertBlockLocalToWorldCell(blockLocalCell);
            var east = new BlockPositionInfo(OriginPos, BlockDirection.East, BlockSize).ConvertBlockLocalToWorldCell(blockLocalCell);

            Assert.AreEqual(OriginPos + blockLocalCell, north, "north placement must map the local cell straight onto the origin");
            Assert.AreNotEqual(north, east, "east placement returned the same world cell as north");
        }
    }
}
