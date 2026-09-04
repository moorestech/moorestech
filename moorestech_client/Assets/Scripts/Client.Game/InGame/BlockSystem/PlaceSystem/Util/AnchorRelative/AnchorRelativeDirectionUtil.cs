using System;
using Game.Block.Interface;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Util.AnchorRelative
{
    /// <summary>
    ///     ローカル向きをアンカー向きで回しワールド向きへ変換
    ///     Maps an anchor-North-basis local direction into world space using the placed anchor's direction
    /// </summary>
    public static class AnchorRelativeDirectionUtil
    {
        private static readonly BlockDirection[] AllDirections = (BlockDirection[])Enum.GetValues(typeof(BlockDirection));

        public static BlockDirection RotateByAnchor(BlockDirection localDirection, BlockDirection anchorDirection)
        {
            // アンカー姿勢とローカル姿勢を合成する
            // Compose the anchor rotation with the local rotation to obtain the world orientation
            var worldRotation = anchorDirection.GetRotation() * localDirection.GetRotation();
            var worldForward = Vector3Int.RoundToInt(worldRotation * Vector3.forward);
            var worldUp = Vector3Int.RoundToInt(worldRotation * Vector3.up);

            // 前方と上方が一致する方位を返す
            // Return the direction whose forward and up axes both match; a horizontal anchor always stays inside this set
            foreach (var candidate in AllDirections)
            {
                var candidateRotation = candidate.GetRotation();
                if (Vector3Int.RoundToInt(candidateRotation * Vector3.forward) != worldForward) continue;
                if (Vector3Int.RoundToInt(candidateRotation * Vector3.up) != worldUp) continue;
                return candidate;
            }

            // 垂直アンカー×垂直ローカル等、12方位で表せない合成は無言で潰さず設定ミスとして落とす
            // A composition outside the 12 directions (vertical anchor times vertical local) is a misconfiguration, not something to swallow
            throw new InvalidOperationException($"Composed block direction is not representable. local:{localDirection} anchor:{anchorDirection}");
        }
    }
}
