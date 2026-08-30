using Core.Master;
using Game.Block.Interface;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.ChainPreview
{
    /// <summary>
    ///     連結ゴースト1件の定義（North基準ローカル）
    ///     One chain ghost definition (North-basis local frame)
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
