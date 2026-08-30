using Core.Master;
using Game.Block.Interface;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.ChainPreview
{
    /// <summary>
    ///     連結ゴースト1件分の定義。offset/directionは設置中ブロックのNorth基準ローカル
    ///     One chain ghost definition; offset/direction are in the being-placed block's North-basis local frame
    /// </summary>
    public readonly struct ChainGhost
    {
        public readonly BlockId BlockId;
        public readonly Vector3Int Offset;
        public readonly BlockDirection LocalDirection;
        
        public ChainGhost(BlockId blockId, Vector3Int offset, BlockDirection localDirection)
        {
            BlockId = blockId;
            Offset = offset;
            LocalDirection = localDirection;
        }
    }
}
