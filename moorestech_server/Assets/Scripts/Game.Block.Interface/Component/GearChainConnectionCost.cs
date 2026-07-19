using System.Collections.Generic;
using Core.Master;

namespace Game.Block.Interface.Component
{
    /// <summary>
    /// 歯車チェーン1接続に消費した素材（複数可）の情報。撤去時の返却に使う
    /// Multi-material consumption info per gear-chain connection, used for refund on removal
    /// </summary>
    public readonly struct GearChainConnectionCost
    {
        public readonly IReadOnlyList<ConnectToolMaterialCost> Materials;

        public GearChainConnectionCost(IReadOnlyList<ConnectToolMaterialCost> materials)
        {
            Materials = materials;
        }

        // プレビュー表示用の総素材数。全素材の消費数を合算する
        // Total material count for preview display; sums consumption across all materials
        public int TotalCount
        {
            get
            {
                if (Materials == null) return 0;
                var total = 0;
                foreach (var material in Materials) total += material.Count;
                return total;
            }
        }
    }
}
