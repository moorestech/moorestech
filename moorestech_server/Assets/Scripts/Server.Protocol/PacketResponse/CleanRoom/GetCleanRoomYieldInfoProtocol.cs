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

            // サーバーの役割ごとの結果を役割ごとの factory へ写す（クライアントは Game.Block の型を持たない）。出力待ちの確定済み抽選は同じ役割の値として添える
            // Map each per-role server result onto its per-role factory, so the client never holds Game.Block types; a fixed draw waiting for output rides along as the same role's values
            static ResponseCleanRoomYieldInfoMessagePack CreateResponse(CleanRoomYieldPreviewReport report)
            {
                var pending = report.PendingDraw;
                return report.Next switch
                {
                    NoRolePreview _ => ResponseCleanRoomYieldInfoMessagePack.CreateNoRole(),
                    // 稼働できない間は率の欄を出さない（停止理由は部屋欄が出す）ので、確定済みの率も載せない
                    // While the machine cannot operate no rate section is shown (the room section shows the halt reason), so fixed rates are not carried either
                    NotOperablePreview _ => ResponseCleanRoomYieldInfoMessagePack.CreateNotOperable(),
                    WaferGradePreview waferGrade => ResponseCleanRoomYieldInfoMessagePack.CreateWaferGrade(waferGrade.MaxGrade, waferGrade.PromotionChance, pending == null ? null : ToWaferGrade(RequireSameRole<WaferGradePreview>(pending))),
                    LayerExposurePreview layerExposure => ResponseCleanRoomYieldInfoMessagePack.CreateLayerExposure(layerExposure.LayerFailRate, pending == null ? null : new LayerExposureYieldMessagePack(RequireSameRole<LayerExposurePreview>(pending).LayerFailRate)),
                    DicingPreview dicing => ResponseCleanRoomYieldInfoMessagePack.CreateDicing(dicing.DieLossRate, pending == null ? null : new DicingYieldMessagePack(RequireSameRole<DicingPreview>(pending).DieLossRate)),
                    _ => throw new ArgumentOutOfRangeException(nameof(report.Next), report.Next.GetType().Name, "未対応の抽選の中身 / Unsupported yield preview"),
                };

                // 確定済み抽選は選択レシピのものだけが届くので役割は次の抽選と同じ。違えば部品の約束の破れなので止める
                // Only the selected recipe's fixed draw arrives, so its role matches the next draw; a mismatch breaks the component's contract, so stop
                T RequireSameRole<T>(CleanRoomYieldPreviewResult pendingDraw) where T : CleanRoomYieldPreviewResult
                {
                    return pendingDraw as T ?? throw new InvalidOperationException($"確定済み抽選の役割が次の抽選と違います next={report.Next.GetType().Name} pending={pendingDraw.GetType().Name}");
                }

                static WaferGradeYieldMessagePack ToWaferGrade(WaferGradePreview preview)
                {
                    return new WaferGradeYieldMessagePack(preview.MaxGrade, preview.PromotionChance);
                }
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

            // 出力の空きを待っている確定済み抽選に使った値。判別子と同じ役割の欄だけが書かれ、待っていなければ全部 null
            // Values a fixed draw waiting for output space used; only the discriminator's role is written, and all stay null when nothing waits
            [Key(6)] public WaferGradeYieldMessagePack PendingWaferGrade { get; set; }
            [Key(7)] public LayerExposureYieldMessagePack PendingLayerExposure { get; set; }
            [Key(8)] public DicingYieldMessagePack PendingDicing { get; set; }

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

            // pendingDraw は出力待ちの確定済み抽選の値。待っていなければ null
            // pendingDraw holds the values of a fixed draw waiting for output; null when nothing waits
            public static ResponseCleanRoomYieldInfoMessagePack CreateWaferGrade(int maxGrade, double promotionChance, WaferGradeYieldMessagePack pendingDraw)
            {
                return new ResponseCleanRoomYieldInfoMessagePack(CleanRoomYieldResponseKind.WaferGrade) { WaferGrade = new WaferGradeYieldMessagePack(maxGrade, promotionChance), PendingWaferGrade = pendingDraw };
            }

            public static ResponseCleanRoomYieldInfoMessagePack CreateLayerExposure(double layerFailRate, LayerExposureYieldMessagePack pendingDraw)
            {
                return new ResponseCleanRoomYieldInfoMessagePack(CleanRoomYieldResponseKind.LayerExposure) { LayerExposure = new LayerExposureYieldMessagePack(layerFailRate), PendingLayerExposure = pendingDraw };
            }

            public static ResponseCleanRoomYieldInfoMessagePack CreateDicing(double dieLossRate, DicingYieldMessagePack pendingDraw)
            {
                return new ResponseCleanRoomYieldInfoMessagePack(CleanRoomYieldResponseKind.Dicing) { Dicing = new DicingYieldMessagePack(dieLossRate), PendingDicing = pendingDraw };
            }

            // 判別子と確定済み抽選の欄の読み分けは読み口1か所（CleanRoomYieldResponseReader）で行う
            // The single reader (CleanRoomYieldResponseReader) branches on the discriminator and the fixed-draw fields
            public BlockInfoReadResult TryReadView(out CleanRoomYieldView view)
            {
                return CleanRoomYieldResponseReader.Read(this, out view);
            }
        }

        #endregion
    }
}
