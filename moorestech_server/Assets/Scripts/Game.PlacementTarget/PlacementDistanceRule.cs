using UnityEngine;

namespace Game.PlacementTarget
{
    public static class PlacementDistanceRule
    {
        private const double MaximumDistance = 100;

        public static bool IsWithinReach(Vector3 playerPosition, Vector3Int origin)
        {
            // 整数境界と非有限な同期座標を安全に判定する
            // Safely judge integer boundaries and non-finite synchronized positions
            var x = (double)origin.x - playerPosition.x;
            var y = (double)origin.y - playerPosition.y;
            var z = (double)origin.z - playerPosition.z;
            return x * x + y * y + z * z <= MaximumDistance * MaximumDistance;
        }
    }
}
