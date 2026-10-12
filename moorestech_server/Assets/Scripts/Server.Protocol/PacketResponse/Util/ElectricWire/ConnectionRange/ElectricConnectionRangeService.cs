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
            var horizontal = Mathf.Max(range.Horizontal, 1);
            var height = Mathf.Max(range.Height, 1);
            return OverlapsAxis(self.MinPos.x, self.MaxPos.x, target.MinPos.x, target.MaxPos.x, horizontal) &&
                   OverlapsAxis(self.MinPos.y, self.MaxPos.y, target.MinPos.y, target.MaxPos.y, height) &&
                   OverlapsAxis(self.MinPos.z, self.MaxPos.z, target.MinPos.z, target.MaxPos.z, horizontal);

            #region Internal

            bool OverlapsAxis(int selfMin, int selfMax, int targetMin, int targetMax, int width)
            {
                // 範囲膨張も整数境界で周回させない
                // Keep range expansion from wrapping at integer boundaries
                var low = width / 2;
                var high = width - 1 - low;
                return targetMin <= (long)selfMax + high && (long)selfMin - low <= targetMax;
            }

            #endregion
        }

    }
}
