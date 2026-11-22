using System;
using System.Collections.Generic;
using Game.Block.Blocks.CleanRoom.Machine.YieldPreview;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Game.Context;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using UnityEngine;

namespace Server.Protocol.PacketResponse
{
    // 室内機械の選択工程について、最終の出力割合と改善の差分を返すプロトコル
    // Protocol returning final outcome odds and improvement deltas for an interior machine’s selected recipe
    public class GetCleanRoomYieldInfoProtocol : IPacketResponse
    {
        public const string ProtocolTag = "va:getCleanRoomYieldInfo";

        public GetCleanRoomYieldInfoProtocol(ServiceProvider serviceProvider)
        {
        }

        public ProtocolMessagePackBase GetResponse(byte[] payload, int requesterPlayerId)
        {
            var request = MessagePackSerializer.Deserialize<RequestCleanRoomYieldInfoMessagePack>(payload);

            // 照会の間にブロックが壊れていれば BlockNotFound を返し、クライアントのポーラーが警告を出す
            // If the block was removed before the query, return BlockNotFound so the client's poller logs a warning
            var block = ServerContext.WorldBlockDatastore.GetBlock(request.BlockInstanceId);
            if (block == null) return ResponseCleanRoomYieldInfoMessagePack.CreateBlockNotFound();

            // クライアントは室内機械にだけ問い合わせる。別種のブロックは食い違いとして警告し、正常な NoRole と区別して返す
            // The client only asks interior machines; any other block is a mismatch, warned and returned apart from a genuine NoRole
            if (!block.TryGetComponent<CleanRoomYieldPreviewComponent>(out var yieldPreview))
            {
                Debug.LogWarning($"[GetCleanRoomYieldInfo] block {request.BlockInstanceId} has no CleanRoomYieldPreviewComponent; returning UnsupportedTarget");
                return ResponseCleanRoomYieldInfoMessagePack.CreateUnsupportedTarget();
            }
            return CleanRoomYieldResponseFactory.Create(yieldPreview.GetPreview());
        }

        #region MessagePack

        [MessagePackObject]
        public class RequestCleanRoomYieldInfoMessagePack : ProtocolMessagePackBase
        {
            [Key(2)] public BlockInstanceId BlockInstanceId { get; set; }

            [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
            public RequestCleanRoomYieldInfoMessagePack() { }

            public RequestCleanRoomYieldInfoMessagePack(BlockInstanceId blockInstanceId)
            {
                Tag = ProtocolTag;
                BlockInstanceId = blockInstanceId;
            }
        }

        [MessagePackObject]
        public class ResponseCleanRoomYieldInfoMessagePack : ProtocolMessagePackBase
        {
            // wire は判別子と役割ごとの入れ子が並ぶ形。その役割の入れ子だけが書かれ、他は null。組み立ては下の factory、読み出しは TryReadView だけ
            // The wire has a discriminator plus per-role nested objects; only that role's object is written and the rest stay null. Build only via the factories below and read only via TryReadView
            [Key(2)] public CleanRoomYieldResponseKind Kind { get; }
            // 画面に出す最終の内訳と改善の行。Key 3〜6 は外した項目の欠番
            // Final odds and improvement rows for the screen. Keys 3–6 are retired gaps
            [Key(7)] public LayerExposureOddsMessagePack LayerExposureOdds { get; }
            [Key(8)] public DicingOddsMessagePack DicingOdds { get; }
            [Key(9)] public List<CleanRoomYieldImprovementMessagePack> Improvements { get; }

            [SerializationConstructor]
            public ResponseCleanRoomYieldInfoMessagePack(string tag, int sequenceId, CleanRoomYieldResponseKind kind,
                LayerExposureOddsMessagePack layerExposureOdds, DicingOddsMessagePack dicingOdds, List<CleanRoomYieldImprovementMessagePack> improvements)
            {
                Tag = tag;
                SequenceId = sequenceId;
                Kind = kind;
                LayerExposureOdds = layerExposureOdds;
                DicingOdds = dicingOdds;
                Improvements = improvements;
            }

            private ResponseCleanRoomYieldInfoMessagePack(CleanRoomYieldResponseKind kind)
                : this(ProtocolTag, 0, kind, null, null, null) { }

            public static ResponseCleanRoomYieldInfoMessagePack CreateBlockNotFound()
            {
                return new ResponseCleanRoomYieldInfoMessagePack(CleanRoomYieldResponseKind.BlockNotFound);
            }

            public static ResponseCleanRoomYieldInfoMessagePack CreateUnsupportedTarget()
            {
                return new ResponseCleanRoomYieldInfoMessagePack(CleanRoomYieldResponseKind.UnsupportedTarget);
            }

            public static ResponseCleanRoomYieldInfoMessagePack CreateNoRole()
            {
                return new ResponseCleanRoomYieldInfoMessagePack(CleanRoomYieldResponseKind.NoRole);
            }

            public static ResponseCleanRoomYieldInfoMessagePack CreateNotOperable()
            {
                return new ResponseCleanRoomYieldInfoMessagePack(CleanRoomYieldResponseKind.NotOperable);
            }

            public static ResponseCleanRoomYieldInfoMessagePack CreateLayerExposure(LayerExposureOddsMessagePack odds, List<CleanRoomYieldImprovementMessagePack> improvements)
            {
                return new ResponseCleanRoomYieldInfoMessagePack(ProtocolTag, 0, CleanRoomYieldResponseKind.LayerExposure, odds, null, improvements);
            }

            public static ResponseCleanRoomYieldInfoMessagePack CreateDicing(DicingOddsMessagePack odds, List<CleanRoomYieldImprovementMessagePack> improvements)
            {
                return new ResponseCleanRoomYieldInfoMessagePack(ProtocolTag, 0, CleanRoomYieldResponseKind.Dicing, null, odds, improvements);
            }

            // 判別子の読み分けは読み口1か所（CleanRoomYieldResponseReader）で行う
            // The single reader (CleanRoomYieldResponseReader) branches on the discriminator
            public BlockInfoReadResult TryReadView(out CleanRoomYieldView view)
            {
                return CleanRoomYieldResponseReader.Read(this, out view);
            }
        }

        #endregion
    }
}
