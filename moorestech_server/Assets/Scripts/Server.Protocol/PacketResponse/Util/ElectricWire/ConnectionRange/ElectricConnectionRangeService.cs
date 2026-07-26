using Game.Block.Interface;
using UnityEngine;

namespace Server.Protocol.PacketResponse.Util.ElectricWire.ConnectionRange
{
    public static class ElectricConnectionRangeService
    {
        /// <summary>
        /// 双方の範囲ボックスが相手の占有AABBと重なる場合のみ接続可とする相互判定
        /// Mutual judgement: connectable only when both range boxes overlap the partner's occupied AABB
        /// </summary>
        public static bool IsMutuallyConnectable(
            BlockPositionInfo aInfo, ConnectionRangeProfile aProfile, bool aIsPole,
            BlockPositionInfo bInfo, ConnectionRangeProfile bProfile, bool bIsPole)
        {
            return Covers(aInfo, aProfile.GetRangeAgainst(bIsPole), bInfo) &&
                   Covers(bInfo, bProfile.GetRangeAgainst(aIsPole), aInfo);
        }

        public static bool Covers(BlockPositionInfo self, (int Horizontal, int Height) range, BlockPositionInfo target)
        {
            var (rangeMin, rangeMax) = CreateBounds();
            return HasOverlap();

            #region Internal

            (Vector3Int min, Vector3Int max) CreateBounds()
            {
                // 占有AABBを低側floor(r/2)・高側r-1-floor(r/2)だけ膨張させる（従来のセル列挙の合併と一致）
                // Inflate the occupied AABB by floor(r/2) low and r-1-floor(r/2) high (matches the union of legacy cell enumeration)
                var horizontal = Mathf.Max(range.Horizontal, 1);
                var height = Mathf.Max(range.Height, 1);
                var lowHorizontal = horizontal / 2;
                var highHorizontal = horizontal - 1 - lowHorizontal;
                var lowHeight = height / 2;
                var highHeight = height - 1 - lowHeight;

                var min = new Vector3Int(self.MinPos.x - lowHorizontal, self.MinPos.y - lowHeight, self.MinPos.z - lowHorizontal);
                var max = new Vector3Int(self.MaxPos.x + highHorizontal, self.MaxPos.y + highHeight, self.MaxPos.z + highHorizontal);
                return (min, max);
            }

            bool HasOverlap()
            {
                return target.MinPos.x <= rangeMax.x && rangeMin.x <= target.MaxPos.x &&
                       target.MinPos.y <= rangeMax.y && rangeMin.y <= target.MaxPos.y &&
                       target.MinPos.z <= rangeMax.z && rangeMin.z <= target.MaxPos.z;
            }

            #endregion
        }

    }
}
