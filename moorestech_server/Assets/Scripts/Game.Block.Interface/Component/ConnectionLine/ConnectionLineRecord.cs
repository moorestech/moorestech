using System;
using System.Collections.Generic;
using Core.Master;

namespace Game.Block.Interface.Component
{
    /// <summary>
    /// 接続線（電線・歯車チェーン）1本の記録。引いた接続ツールの種類と払った素材を持ち、返却とUndoの引き直しに使う
    /// Record of one connection line (wire or gear chain): the connect tool it was drawn with and the paid materials, used for refunds and undo re-drawing
    /// </summary>
    public readonly struct ConnectionLineRecord
    {
        public readonly Guid ConnectToolGuid;
        public readonly IReadOnlyList<ConnectToolMaterialCost> Materials;

        public ConnectionLineRecord(Guid connectToolGuid, IReadOnlyList<ConnectToolMaterialCost> materials)
        {
            ConnectToolGuid = connectToolGuid;
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
