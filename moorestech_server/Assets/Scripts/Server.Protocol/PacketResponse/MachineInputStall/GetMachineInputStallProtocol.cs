using System;
using Game.Block.Blocks.Machine.Inventory.Input;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Game.Context;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using UnityEngine;

namespace Server.Protocol.PacketResponse
{
    // 継承元レベルによる搬入停止を照会
    // Queries inflow stalls caused by the source level
    public class GetMachineInputStallProtocol : IPacketResponse
    {
        public const string ProtocolTag = "va:getMachineInputStall";

        public GetMachineInputStallProtocol(ServiceProvider serviceProvider)
        {
        }

        public ProtocolMessagePackBase GetResponse(byte[] payload, int requesterPlayerId)
        {
            var request = MessagePackSerializer.Deserialize<RequestMachineInputStallMessagePack>(payload);

            // 照会の間にブロックが壊れていれば BlockNotFound を返し、クライアントのポーラーが警告を出す
            // If the block was removed before the query, return BlockNotFound so the client's poller logs a warning
            var block = ServerContext.WorldBlockDatastore.GetBlock(request.BlockInstanceId);
            if (block == null) return ResponseMachineInputStallMessagePack.CreateBlockNotFound();

            // クライアントは機械にだけ問い合わせる。別種のブロックは食い違いとして警告し、正常な NotStalled と区別して返す
            // The client only asks machines; any other block is a mismatch, warned and returned apart from a genuine NotStalled
            if (!block.TryGetComponent<MachineInputLevelStallComponent>(out var stallComponent))
            {
                Debug.LogWarning($"[GetMachineInputStall] block {request.BlockInstanceId} has no MachineInputLevelStallComponent; returning UnsupportedTarget");
                return ResponseMachineInputStallMessagePack.CreateUnsupportedTarget();
            }
            if (!stallComponent.TryGetStall(out var stall)) return ResponseMachineInputStallMessagePack.CreateNotStalled();
            return ResponseMachineInputStallMessagePack.CreateStalled(ToWireReason(stall.Reason), stall.WaitingItemId.AsPrimitive());

            #region Internal

            // サーバーの理由を wire の理由へ写す（クライアントは Game.Block の型を wire で受けない）
            // Map the server reason onto the wire reason, so the client never receives Game.Block types on the wire
            static MachineInputStallReasonKind ToWireReason(MachineInputLevelRefusalReason reason)
            {
                return reason switch
                {
                    MachineInputLevelRefusalReason.MixedLevel => MachineInputStallReasonKind.MixedLevel,
                    MachineInputLevelRefusalReason.AboveCraftCap => MachineInputStallReasonKind.AboveCraftCap,
                    _ => throw new ArgumentOutOfRangeException(nameof(reason), reason, "未対応の停止理由 / Unsupported stall reason"),
                };
            }

            #endregion
        }

        #region MessagePack

        [MessagePackObject]
        public class RequestMachineInputStallMessagePack : ProtocolMessagePackBase
        {
            [Key(2)] public BlockInstanceId BlockInstanceId { get; set; }

            [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
            public RequestMachineInputStallMessagePack() { }

            public RequestMachineInputStallMessagePack(BlockInstanceId blockInstanceId)
            {
                Tag = ProtocolTag;
                BlockInstanceId = blockInstanceId;
            }
        }

        [MessagePackObject]
        public class ResponseMachineInputStallMessagePack : ProtocolMessagePackBase
        {
            // wire は判別子と、Stalled のときだけ書かれる中身。組み立ては下の factory、読み出しは TryReadView だけ
            // The wire has a discriminator plus details written only when Stalled; build only via the factories below and read only via TryReadView
            [Key(2)] public MachineInputStallResponseKind Kind { get; set; }
            [Key(3)] public MachineInputStallMessagePack Stall { get; set; }

            [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
            public ResponseMachineInputStallMessagePack() { }

            private ResponseMachineInputStallMessagePack(MachineInputStallResponseKind kind)
            {
                Tag = ProtocolTag;
                Kind = kind;
            }

            public static ResponseMachineInputStallMessagePack CreateBlockNotFound()
            {
                return new ResponseMachineInputStallMessagePack(MachineInputStallResponseKind.BlockNotFound);
            }

            public static ResponseMachineInputStallMessagePack CreateUnsupportedTarget()
            {
                return new ResponseMachineInputStallMessagePack(MachineInputStallResponseKind.UnsupportedTarget);
            }

            public static ResponseMachineInputStallMessagePack CreateNotStalled()
            {
                return new ResponseMachineInputStallMessagePack(MachineInputStallResponseKind.NotStalled);
            }

            public static ResponseMachineInputStallMessagePack CreateStalled(MachineInputStallReasonKind reason, int waitingItemId)
            {
                return new ResponseMachineInputStallMessagePack(MachineInputStallResponseKind.Stalled) { Stall = new MachineInputStallMessagePack(reason, waitingItemId) };
            }

            // 判別子の読み分けはここだけで行う。消失・非対応の対象はそのまま返し、中身の欠けた・理由の読めない Stalled と未知の種別は警告して Unreadable
            // The only place that branches on the discriminator; vanished and unsupported targets pass through, a broken Stalled and unknown kinds warn and are Unreadable
            public BlockInfoReadResult TryReadView(out MachineInputStallView view)
            {
                view = default;
                switch (Kind)
                {
                    case MachineInputStallResponseKind.NotStalled:
                        view = MachineInputNotStalledView.Instance;
                        return BlockInfoReadResult.Success;
                    case MachineInputStallResponseKind.Stalled:
                        if (Stall == null || !Enum.IsDefined(typeof(MachineInputStallReasonKind), Stall.Reason)) return WarnUnreadable();
                        view = new MachineInputStalledView(Stall);
                        return BlockInfoReadResult.Success;
                    case MachineInputStallResponseKind.BlockNotFound:
                        return BlockInfoReadResult.BlockNotFound;
                    case MachineInputStallResponseKind.UnsupportedTarget:
                        return BlockInfoReadResult.UnsupportedTarget;
                    default:
                        Debug.LogWarning($"Unreadable response kind: {Kind}");
                        return BlockInfoReadResult.Unreadable;
                }

                #region Internal

                // 外部から届いた wire の食い違いなので例外にせず、警告を出して読めない応答として扱う（ポーラーは値を捨てて再試行する）
                // A mismatch in wire data from outside, so warn instead of throwing and treat it as unreadable (the poller drops the value and retries)
                BlockInfoReadResult WarnUnreadable()
                {
                    Debug.LogWarning($"[GetMachineInputStall] Stalled response is unreadable (missing details or unknown reason {Stall?.Reason}); treating it as unreadable");
                    return BlockInfoReadResult.Unreadable;
                }

                #endregion
            }
        }

        #endregion
    }
}
