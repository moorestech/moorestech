using System.Collections.Generic;
using Core.Master;
using Mooresmaster.Model.BlocksModule;
using UnityEngine;

namespace Game.Block.Interface.Extension
{
    public static class BlockPositionInfoExtension
    {
        public static bool IsOverlap(this BlockPositionInfo self, BlockPositionInfo other)
        {
            return self.MinPos.x <= other.MaxPos.x && other.MinPos.x <= self.MaxPos.x &&
                   self.MinPos.y <= other.MaxPos.y && other.MinPos.y <= self.MaxPos.y &&
                   self.MinPos.z <= other.MaxPos.z && other.MinPos.z <= self.MaxPos.z;
        }

        public static IEnumerable<Vector3Int> EnumeratePositions(this BlockPositionInfo self)
        {
            // ブロックが占有する全セルを列挙する
            // Enumerate every grid cell occupied by the block
            for (var x = self.MinPos.x; x <= self.MaxPos.x; x++)
            for (var y = self.MinPos.y; y <= self.MaxPos.y; y++)
            for (var z = self.MinPos.z; z <= self.MaxPos.z; z++)
                yield return new Vector3Int(x, y, z);
        }

        /// <summary>
        ///     鉱脈の上かを判定するセル列。採掘機は実際に掘るドリルセルだけ、他は占有セル全域を見る
        ///     Cells judged against a vein: a miner's actual drill cell only, every occupied cell otherwise
        ///     この規則の正本はここ1箇所で、サーバーのチャレンジ判定とクライアントの設置制限が同じものを呼ぶ
        ///     This is the single source of the rule, called by both the server challenge check and the client placement restriction
        /// </summary>
        public static IEnumerable<Vector3Int> EnumerateVeinJudgeCells(this BlockPositionInfo self, BlockMasterElement blockMaster)
        {
            if (blockMaster.BlockParam is IMinerParam minerParam)
            {
                yield return self.ConvertBlockLocalToWorldCell(minerParam.DrillLocalPosition);
                yield break;
            }

            foreach (var cell in self.EnumeratePositions()) yield return cell;
        }
    }
}
