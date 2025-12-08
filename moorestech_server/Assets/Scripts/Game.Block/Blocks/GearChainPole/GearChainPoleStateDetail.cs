using System;
using System.Collections.Generic;
using System.Linq;
using Game.Block.Interface;
using MessagePack;
using Server.Util.MessagePack;
using UnityEngine;

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

        /// <summary>
        /// 接続先のブロック座標のリスト（クライアント側で使用）
        /// List of connected block positions (used by client)
        /// </summary>
        [Key(1)] public Vector3IntMessagePack[] PartnerBlockPositions { get; set; }

        public GearChainPoleStateDetail(IEnumerable<BlockInstanceId> partnerIds, IEnumerable<Vector3Int> partnerPositions)
        {
            // 接続先IDをプリミティブ型の配列に変換する
            // Convert partner IDs to primitive array
            PartnerBlockInstanceIds = partnerIds.Select(id => id.AsPrimitive()).ToArray();

            // 接続先座標をMessagePack用の型に変換する
            // Convert partner positions to MessagePack type
            PartnerBlockPositions = partnerPositions.Select(pos => new Vector3IntMessagePack(pos)).ToArray();
        }

        [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
        public GearChainPoleStateDetail()
        {
        }
    }
}
