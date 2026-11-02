using System.Collections.Generic;
using Game.Block.Interface;
using NUnit.Framework;
using UnityEngine;

namespace Tests.UnitTest.Game.BlockGeometry
{
    /// <summary>
    ///     全12向きで、回したモデルの箱とローカルセルの換算がブロックの占有範囲に一致することを確かめる
    ///     Verifies for all 12 directions that the rotated model box and the local-cell conversion match the occupied range
    /// </summary>
    public class BlockDirectionModelOriginTest
    {
        private static readonly Vector3Int OriginPos = new(10, 4, -7);

        // 高さが1でない寸法でないと上向きの原点ずれが出ないので、3辺とも違う寸法と1×1×1を使う
        // Only a height other than 1 exposes up-facing origin errors, so use three distinct sides plus 1x1x1
        private static readonly Vector3Int[] BlockSizes = { new(2, 3, 5), Vector3Int.one };

        [Test]
        public void 回したモデルの箱が占有範囲と一致する([Values] BlockDirection direction, [ValueSource(nameof(BlockSizes))] Vector3Int blockSize)
        {
            var positionInfo = new BlockPositionInfo(OriginPos, direction, blockSize);

            // モデルは [0,size] に収まる前提なので、8隅を回してモデル原点を足した箱を求める
            // Models fit in [0,size], so rotate the 8 corners and add the model origin to get the box
            var rotation = Matrix4x4.Rotate(direction.GetRotation());
            var modelOrigin = direction.GetBlockModelOriginPos(OriginPos, blockSize);
            var min = Vector3Int.one * int.MaxValue;
            var max = Vector3Int.one * int.MinValue;
            for (var corner = 0; corner < 8; corner++)
            {
                var local = new Vector3((corner & 1) * blockSize.x, ((corner >> 1) & 1) * blockSize.y, ((corner >> 2) & 1) * blockSize.z);
                var world = modelOrigin + Vector3Int.RoundToInt(rotation.MultiplyPoint3x4(local));
                min = Vector3Int.Min(min, world);
                max = Vector3Int.Max(max, world);
            }

            Assert.AreEqual(positionInfo.MinPos, min, $"model box min for {direction} {blockSize}");
            Assert.AreEqual(positionInfo.MaxPos + Vector3Int.one, max, $"model box max for {direction} {blockSize}");
        }

        [Test]
        public void 全ローカルセルの換算結果が占有範囲と一致する([Values] BlockDirection direction, [ValueSource(nameof(BlockSizes))] Vector3Int blockSize)
        {
            var positionInfo = new BlockPositionInfo(OriginPos, direction, blockSize);

            // 占有範囲の全セルと、ローカル全セルの換算結果が同じ集合になる
            // Every occupied cell and the converted local cells must form the same set
            var occupiedCells = new HashSet<Vector3Int>();
            for (var x = positionInfo.MinPos.x; x <= positionInfo.MaxPos.x; x++)
            for (var y = positionInfo.MinPos.y; y <= positionInfo.MaxPos.y; y++)
            for (var z = positionInfo.MinPos.z; z <= positionInfo.MaxPos.z; z++)
                occupiedCells.Add(new Vector3Int(x, y, z));

            var convertedCells = new HashSet<Vector3Int>();
            for (var x = 0; x < blockSize.x; x++)
            for (var y = 0; y < blockSize.y; y++)
            for (var z = 0; z < blockSize.z; z++)
                convertedCells.Add(positionInfo.ConvertBlockLocalToWorldCell(new Vector3Int(x, y, z)));

            CollectionAssert.AreEquivalent(occupiedCells, convertedCells, $"local cells do not cover the footprint for {direction} {blockSize}");
        }
    }
}
