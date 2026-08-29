using System.Collections.Generic;
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
        ///     「鉱脈の上か」の唯一の規則。底面フットプリントと鉱脈AABBのXZ重なりだけを見る（ADR 0039）
        ///     The single rule for "is it over a vein": the footprint and the vein AABB overlapping in XZ, nothing else (ADR 0039)
        ///     ブロックは地表に置く前提なので、斜面でfloor(hit.y)が鉱脈AABBのYから外れても掘れる／達成する
        ///     Blocks sit on the surface, so a slope pushing floor(hit.y) outside the vein's Y range must not block mining or completion
        ///     クライアントの設置制限・サーバーの採掘対象・チャレンジ達成判定がこの1本を共有する
        ///     The client placement restriction, the server mining target and the challenge completion check all share this one rule
        /// </summary>
        public static bool OverlapsVeinXz(this BlockPositionInfo self, Vector3Int veinMinCell, Vector3Int veinMaxCell)
        {
            return self.MinPos.x <= veinMaxCell.x && veinMinCell.x <= self.MaxPos.x &&
                   self.MinPos.z <= veinMaxCell.z && veinMinCell.z <= self.MaxPos.z;
        }
    }
}
