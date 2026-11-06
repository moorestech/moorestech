using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Game.SaveLoad.Migration.Steps.V9ToV10
{
    /// <summary>
    /// 版9の12向きの番号（Up 0..3・水平 4..7・Down 8..11、各組は北東南西の順）を版10の水平番号（北0東1南2西3）へ読む。現行の BlockDirection は参照しない
    /// Reads the version-9 twelve-direction numbers (Up 0..3, flat 4..7, Down 8..11, each group N/E/S/W) as version-10 horizontal numbers (N0 E1 S2 W3); the live BlockDirection is never referenced
    /// </summary>
    internal static class LegacyTwelveDirection
    {
        public static bool TryRead(JToken directionToken, out int horizontal, out bool wasVertical)
        {
            horizontal = 0;
            wasVertical = false;
            if (directionToken == null || directionToken.Type != JTokenType.Integer) return false;
            var legacy = directionToken.Value<long>();
            if (legacy < 0 || 11 < legacy) return false;

            // 縦は同じ方位の水平へ寝かせる
            // A vertical direction lies flat on the same heading
            horizontal = (int)(legacy % 4);
            wasVertical = legacy < 4 || 8 <= legacy;
            return true;
        }

        // 占有セルを列挙（東西はxz入替）
        // Enumerate occupied cells (east/west swap x and z)
        public static IEnumerable<Vector3Int> OccupiedCells(Vector3Int minCorner, int horizontal, Vector3Int blockSize)
        {
            var isEastOrWest = horizontal == 1 || horizontal == 3;
            var extent = isEastOrWest ? new Vector3Int(blockSize.z, blockSize.y, blockSize.x) : blockSize;
            for (var x = 0; x < extent.x; x++)
            for (var y = 0; y < extent.y; y++)
            for (var z = 0; z < extent.z; z++)
                yield return minCorner + new Vector3Int(x, y, z);
        }
    }
}
