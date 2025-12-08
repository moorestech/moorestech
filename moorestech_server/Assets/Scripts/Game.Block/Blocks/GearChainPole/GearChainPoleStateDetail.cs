using System;
using System.Collections.Generic;
using System.Linq;
using Game.Block.Interface;
using Game.Context;
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
        /// 接続先のブロック位置リスト（クライアント側で使用）
        /// List of connected block positions (used on client side)
        /// </summary>
        [Key(1)] public Vector3IntMessagePack[] PartnerPositions { get; set; }

        public GearChainPoleStateDetail(IEnumerable<BlockInstanceId> partnerIds)
        {
            // 接続先IDをプリミティブ型の配列に変換する
            // Convert partner IDs to primitive array
            var partnerIdList = partnerIds.ToList();
            PartnerBlockInstanceIds = partnerIdList.Select(id => id.AsPrimitive()).ToArray();

            // BlockInstanceIdから位置を解決してクライアント用データを作成する
            // Resolve positions from BlockInstanceIds and create client-side data
            var positions = new List<Vector3IntMessagePack>();
            foreach (var partnerId in partnerIdList)
            {
                // ブロックが存在する場合のみ位置を追加する
                // Only add position if block exists
                if (!ServerContext.WorldBlockDatastore.BlockMasterDictionary.ContainsKey(partnerId)) continue;

                var position = ServerContext.WorldBlockDatastore.GetBlockPosition(partnerId);
                positions.Add(new Vector3IntMessagePack(position));
            }
            PartnerPositions = positions.ToArray();
        }

        [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
        public GearChainPoleStateDetail()
        {
        }
    }
}

