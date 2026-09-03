using Game.Block.Interface;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Util
{
    /// <summary>
    ///     ローカル原点を占有域ごと回しワールド原点へ変換
    ///     Maps an anchor-North-basis local origin into world space by rotating the target block's whole footprint
    /// </summary>
    public static class AnchorRelativeOriginUtil
    {
        public static Vector3Int ResolveWorldOrigin(BlockPositionInfo anchorFootprint, Vector3Int localOffset, BlockDirection localDirection, Vector3Int targetBlockSize)
        {
            // OriginalPosは常に占有域の最小角。点回転では多セルブロックの原点がズレるため、両角を回して最小角を取り直す
            // OriginalPos is always the footprint's min corner; rotating a single point misplaces multi-cell origins, so rotate both corners and re-take the min
            var localFootprint = new BlockPositionInfo(localOffset, localDirection, targetBlockSize);
            var cornerA = anchorFootprint.ConvertBlockLocalToWorldCell(localFootprint.MinPos);
            var cornerB = anchorFootprint.ConvertBlockLocalToWorldCell(localFootprint.MaxPos);
            return Vector3Int.Min(cornerA, cornerB);
        }
    }
}
