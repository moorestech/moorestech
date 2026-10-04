using System;
using Game.Block.Blocks.ConnectionLine;
using MessagePack;

namespace Game.Block.Blocks.GearChainPole
{
    /// <summary>
    /// チェーンポールのステート詳細データ
    /// Gear chain pole state detail data
    /// </summary>
    [Serializable]
    [MessagePackObject]
    public class GearChainPoleStateDetail
    {
        public const string BlockStateDetailKey = "GearChainPole";

        /// <summary>
        /// 接続先ごとの同期データ（接続先IDと引いた種類）
        /// Per-partner sync data (partner id and the tool it was drawn with)
        /// </summary>
        [Key(0)] public ConnectionLinePartnerMessagePack[] Partners;

        public GearChainPoleStateDetail(ConnectionLinePartnerMessagePack[] partners)
        {
            Partners = partners;
        }

        [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
        public GearChainPoleStateDetail()
        {
        }
    }
}
