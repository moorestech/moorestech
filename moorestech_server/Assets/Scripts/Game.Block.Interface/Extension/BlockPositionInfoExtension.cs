using System.Collections.Generic;
using UnityEngine;

namespace Game.Block.Interface.Extension
{
    public static class BlockPositionInfoExtension
    {
        public static bool IsContainPos(this BlockPositionInfo self, Vector3Int pos)
        {
            return self.MinPos.x <= pos.x && pos.x <= self.MaxPos.x &&
                   self.MinPos.y <= pos.y && pos.y <= self.MaxPos.y &&
                   self.MinPos.z <= pos.z && pos.z <= self.MaxPos.z;
        }
        
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
    }
}
