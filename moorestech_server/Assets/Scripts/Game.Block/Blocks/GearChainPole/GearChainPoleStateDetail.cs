using System;
using System.Collections.Generic;
using System.Linq;
using Game.Block.Interface;
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
        /// 接続先のブロックインスタンスIDのリスト
        /// List of connected block instance IDs
        /// </summary>
        [Key(0)] public int[] PartnerBlockInstanceIds { get; set; }
        
        public GearChainPoleStateDetail(IEnumerable<BlockInstanceId> partnerIds)
        {
            // 接続先IDをプリミティブ型の配列に変換する
            // Convert partner IDs to primitive array
            PartnerBlockInstanceIds = partnerIds.Select(id => id.AsPrimitive()).ToArray();
        }
        
        [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
        public GearChainPoleStateDetail()
        {
        }
    }
}

