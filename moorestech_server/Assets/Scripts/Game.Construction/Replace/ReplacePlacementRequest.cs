using Core.Inventory;
using Core.Master;
using Game.Block.Interface;
using UnityEngine;

namespace Game.Construction
{
    /// <summary>
    /// 1セル分の張替え要求。プロトコルの受信形式を持ち込まないため、設置層の語彙だけで組み立てる
    /// One cell's replace request, built from placement vocabulary alone so no protocol wire format leaks into this layer
    /// </summary>
    public readonly struct ReplacePlacementRequest
    {
        public readonly Vector3Int Position;
        public readonly BlockId BlockId;
        public readonly BlockCreateParam[] CreateParams;
        public readonly IOpenableInventory Inventory;
        public readonly int PlayerId;
        public readonly bool IsFreePlacement;

        public ReplacePlacementRequest(Vector3Int position, BlockId blockId, BlockCreateParam[] createParams, IOpenableInventory inventory, int playerId, bool isFreePlacement)
        {
            Position = position;
            BlockId = blockId;
            CreateParams = createParams;
            Inventory = inventory;
            PlayerId = playerId;
            IsFreePlacement = isFreePlacement;
        }
    }
}
