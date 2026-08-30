using System;
using Game.Block.Interface;
using UnityEngine;

namespace Client.Game.InGame.Tutorial.PlacementGuide
{
    /// <summary>
    ///     アンカーNorth基準のローカル向きを、設置済みアンカーの向きで回してワールド向きへ写す
    ///     Maps an anchor-North-basis local direction into world space using the placed anchor's direction
    /// </summary>
    public static class AnchorRelativeDirectionUtil
    {
        private static readonly BlockDirection[] HorizontalDirections =
        {
            BlockDirection.North, BlockDirection.East, BlockDirection.South, BlockDirection.West,
        };
        
        public static BlockDirection RotateByAnchor(BlockDirection localDirection, BlockDirection anchorDirection)
        {
            // チュートリアルの水平配置のみ対象。垂直系はそのまま通す
            // Only horizontal tutorial layouts are rotated; vertical variants pass through
            if (Array.IndexOf(HorizontalDirections, localDirection) < 0) return localDirection;
            if (Array.IndexOf(HorizontalDirections, anchorDirection) < 0) return localDirection;
            
            // 前方ベクトルをアンカーの回転で回し、一致する水平方位へ写す
            // Rotate the forward vector by the anchor rotation and map it back to a horizontal direction
            var rotate = anchorDirection.GetCoordinateConvertAction();
            var worldForward = rotate(localDirection.GetCoordinateConvertAction()(Vector3Int.forward));
            foreach (var candidate in HorizontalDirections)
                if (candidate.GetCoordinateConvertAction()(Vector3Int.forward) == worldForward)
                    return candidate;
            return localDirection;
        }
    }
}
