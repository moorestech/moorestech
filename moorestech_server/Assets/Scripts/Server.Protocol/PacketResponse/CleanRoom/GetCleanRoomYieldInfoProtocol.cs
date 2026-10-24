using System;
using Game.Block.Blocks.CleanRoom.Machine.YieldPreview;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Game.Context;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using UnityEngine;

namespace Server.Protocol.PacketResponse
{
    // 室内機械の選択工程について、次の抽選に使う上限グレード・昇格率・失敗率・損失率を返すプロトコル
    // Protocol returning the max grade, promotion chance, fail rate and loss rate the next draw of an interior machine will use
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
            return CreateResponse(yieldPreview.GetPreview());

            #region Internal

            // サーバーの役割ごとの結果を役割ごとの factory へ写す（クライアントは Game.Block の型を持たない）
            // Map each per-role server result onto its per-role factory, so the client never holds Game.Block types
            static ResponseCleanRoomYieldInfoMessagePack CreateResponse(CleanRoomYieldPreviewResult preview)
            {
                return preview switch
                {
                    NoRolePreview _ => ResponseCleanRoomYieldInfoMessagePack.CreateNoRole(),
                    NotOperablePreview _ => ResponseCleanRoomYieldInfoMessagePack.CreateNotOperable(),
                    WaferGradePreview waferGrade => ResponseCleanRoomYieldInfoMessagePack.CreateWaferGrade(waferGrade.MaxGrade, waferGrade.PromotionChance),
                    LayerExposurePreview layerExposure => ResponseCleanRoomYieldInfoMessagePack.CreateLayerExposure(layerExposure.LayerFailRate),
                    DicingPreview dicing => ResponseCleanRoomYieldInfoMessagePack.CreateDicing(dicing.DieLossRate),
                    _ => throw new ArgumentOutOfRangeException(nameof(preview), preview.GetType().Name, "未対応の抽選の中身 / Unsupported yield preview"),
                };
            }

            #endregion
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
            [Key(2)] public CleanRoomYieldResponseKind Kind { get; set; }
            [Key(3)] public WaferGradeYieldMessagePack WaferGrade { get; set; }
            [Key(4)] public LayerExposureYieldMessagePack LayerExposure { get; set; }
            [Key(5)] public DicingYieldMessagePack Dicing { get; set; }

            [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
            public ResponseCleanRoomYieldInfoMessagePack() { }

            private ResponseCleanRoomYieldInfoMessagePack(CleanRoomYieldResponseKind kind)
            {
                Tag = ProtocolTag;
                Kind = kind;
            }

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

            public static ResponseCleanRoomYieldInfoMessagePack CreateWaferGrade(int maxGrade, double promotionChance)
            {
                return new ResponseCleanRoomYieldInfoMessagePack(CleanRoomYieldResponseKind.WaferGrade) { WaferGrade = new WaferGradeYieldMessagePack(maxGrade, promotionChance) };
            }

            public static ResponseCleanRoomYieldInfoMessagePack CreateLayerExposure(double layerFailRate)
            {
                return new ResponseCleanRoomYieldInfoMessagePack(CleanRoomYieldResponseKind.LayerExposure) { LayerExposure = new LayerExposureYieldMessagePack(layerFailRate) };
            }

            public static ResponseCleanRoomYieldInfoMessagePack CreateDicing(double dieLossRate)
            {
                return new ResponseCleanRoomYieldInfoMessagePack(CleanRoomYieldResponseKind.Dicing) { Dicing = new DicingYieldMessagePack(dieLossRate) };
            }

            // 判別子の読み分けはここだけで行う。消失・非対応の対象はそのまま返し、入れ子の欠けた応答と未知の種別は警告して Unreadable
            // The only place that branches on the discriminator; vanished and unsupported targets pass through, missing nested objects and unknown kinds warn and are Unreadable
            public BlockInfoReadResult TryReadView(out CleanRoomYieldView view)
            {
                view = default;
                switch (Kind)
                {
                    case CleanRoomYieldResponseKind.NoRole:
                        view = CleanRoomYieldNoRoleView.Instance;
                        return BlockInfoReadResult.Success;
                    case CleanRoomYieldResponseKind.NotOperable:
                        view = CleanRoomYieldNotOperableView.Instance;
                        return BlockInfoReadResult.Success;
                    case CleanRoomYieldResponseKind.WaferGrade:
                        if (WaferGrade == null) return WarnMissingNested();
                        view = new CleanRoomYieldWaferGradeView(WaferGrade);
                        return BlockInfoReadResult.Success;
                    case CleanRoomYieldResponseKind.LayerExposure:
                        if (LayerExposure == null) return WarnMissingNested();
                        view = new CleanRoomYieldLayerExposureView(LayerExposure);
                        return BlockInfoReadResult.Success;
                    case CleanRoomYieldResponseKind.Dicing:
                        if (Dicing == null) return WarnMissingNested();
                        view = new CleanRoomYieldDicingView(Dicing);
                        return BlockInfoReadResult.Success;
                    case CleanRoomYieldResponseKind.BlockNotFound:
                        return BlockInfoReadResult.BlockNotFound;
                    case CleanRoomYieldResponseKind.UnsupportedTarget:
                        return BlockInfoReadResult.UnsupportedTarget;
                    default:
                        Debug.LogWarning($"Unreadable response kind: {Kind}");
                        return BlockInfoReadResult.Unreadable;
                }

                #region Internal

                // 外部から届いた wire の食い違いなので例外にせず、警告を出して読めない応答として扱う（ポーラーは値を捨てて再試行する）
                // A mismatch in wire data from outside, so warn instead of throwing and treat it as unreadable (the poller drops the value and retries)
                BlockInfoReadResult WarnMissingNested()
                {
                    Debug.LogWarning($"[GetCleanRoomYieldInfo] {Kind} response has no {Kind} details; treating it as unreadable");
                    return BlockInfoReadResult.Unreadable;
                }

                #endregion
            }
        }

        #endregion
    }
}
