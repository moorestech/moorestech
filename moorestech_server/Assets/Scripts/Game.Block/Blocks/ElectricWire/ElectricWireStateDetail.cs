using System;
using Game.Block.Blocks.ConnectionLine;
using MessagePack;

namespace Game.Block.Blocks.ElectricWire
{
    /// <summary>
    /// 電力ワイヤーコネクターのステート詳細データ
    /// Electric wire connector state detail data
    /// </summary>
    [Serializable]
    [MessagePackObject]
    public class ElectricWireStateDetail
    {
        public const string BlockStateDetailKey = "ElectricWire";

        /// <summary>
        /// 接続先ごとの同期データ
        /// Per-partner sync data
        /// </summary>
        [Key(0)] public ConnectionLinePartnerMessagePack[] Partners;

        public ElectricWireStateDetail(ConnectionLinePartnerMessagePack[] partners)
        {
            Partners = partners;
        }

        [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
        public ElectricWireStateDetail()
        {
        }
    }
}
