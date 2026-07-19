using System;
using System.Collections.Generic;
using Core.Master;

namespace Game.EnergySystem
{
    /// <summary>
    /// ワイヤー1本の接続に消費した素材（複数可）の情報。切断・撤去時の返却に使う
    /// Multi-material consumption info per wire, used for refund on disconnect or removal
    /// </summary>
    public readonly struct ElectricWireConnectionCost
    {
        public readonly IReadOnlyList<ConnectToolMaterialCost> Materials;

        public ElectricWireConnectionCost(IReadOnlyList<ConnectToolMaterialCost> materials)
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

        public static ElectricWireConnectionCost Empty => new(Array.Empty<ConnectToolMaterialCost>());
    }
}
