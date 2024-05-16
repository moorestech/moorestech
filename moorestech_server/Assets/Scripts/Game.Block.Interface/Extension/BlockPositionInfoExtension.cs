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
    }
}