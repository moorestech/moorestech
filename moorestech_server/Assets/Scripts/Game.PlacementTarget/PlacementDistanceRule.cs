using UnityEngine;

namespace Game.PlacementTarget
{
    public static class PlacementDistanceRule
    {
        private const double MaximumDistance = 100;

        // サーバー検証用: 位置同期の遅れと分割送信の待ちを吸収する許容幅
        // Server-side tolerance absorbing position sync lag and chunked-send waits
        private const double ServerSyncTolerance = 10;

        public static bool IsWithinReach(Vector3 playerPosition, Vector3Int origin)
        {
            return IsWithin(playerPosition, origin, MaximumDistance);
        }

        public static bool IsWithinReachWithSyncTolerance(Vector3 playerPosition, Vector3Int origin)
        {
            return IsWithin(playerPosition, origin, MaximumDistance + ServerSyncTolerance);
        }

        private static bool IsWithin(Vector3 playerPosition, Vector3Int origin, double maximumDistance)
        {
            // 整数境界と非有限な同期座標を安全に判定する
            // Safely judge integer boundaries and non-finite synchronized positions
            var x = (double)origin.x - playerPosition.x;
            var y = (double)origin.y - playerPosition.y;
            var z = (double)origin.z - playerPosition.z;
            return x * x + y * y + z * z <= maximumDistance * maximumDistance;
        }
    }
}
