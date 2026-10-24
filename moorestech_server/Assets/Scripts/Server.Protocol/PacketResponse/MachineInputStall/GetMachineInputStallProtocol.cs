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

            // クライアントは機械にだけ問い合わせる。別種のブロックは食い違いとして警告する
            // The client only asks machines; any other block is a mismatch and gets a warning
            if (!block.TryGetComponent<MachineInputLevelStallComponent>(out var stallComponent))
            {
                Debug.LogWarning($"[GetMachineInputStall] block {request.BlockInstanceId} has no MachineInputLevelStallComponent; returning NotStalled");
                return ResponseMachineInputStallMessagePack.CreateNotStalled();
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
            // wire は判別子と、Stalled のときだけ書かれる中身。組み立ては下の factory、読み出しは TryGetView だけ
            // The wire has a discriminator plus details written only when Stalled; build only via the factories below and read only via TryGetView
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

            public static ResponseMachineInputStallMessagePack CreateNotStalled()
            {
                return new ResponseMachineInputStallMessagePack(MachineInputStallResponseKind.NotStalled);
            }

            public static ResponseMachineInputStallMessagePack CreateStalled(MachineInputStallReasonKind reason, int waitingItemId)
            {
                return new ResponseMachineInputStallMessagePack(MachineInputStallResponseKind.Stalled) { Stall = new MachineInputStallMessagePack(reason, waitingItemId) };
            }

            // BlockNotFound と、中身の欠けた・理由の読めない Stalled は false。判別子の読み分けはここだけで行う
            // False for BlockNotFound and for a Stalled response with missing details or an unknown reason; the only place that branches on the discriminator
            public bool TryGetView(out MachineInputStallView view)
            {
                switch (Kind)
                {
                    case MachineInputStallResponseKind.NotStalled:
                        view = MachineInputNotStalledView.Instance;
                        return true;
                    case MachineInputStallResponseKind.Stalled:
                        if (Stall == null || !Enum.IsDefined(typeof(MachineInputStallReasonKind), Stall.Reason)) return WarnUnreadable(out view);
                        view = new MachineInputStalledView(Stall);
                        return true;
                    case MachineInputStallResponseKind.BlockNotFound:
                        view = default;
                        return false;
                    default:
                        Debug.LogWarning($"Unreadable response kind: {Kind}");
                        view = default;
                        return false;
                }

                #region Internal

                // 外部から届いた wire の食い違いなので例外にせず、警告を出して読めない応答として扱う（ポーラーは値を捨てて止める）
                // A mismatch in wire data from outside, so warn instead of throwing and treat it as unreadable (the poller drops the value and stops)
                bool WarnUnreadable(out MachineInputStallView unreadableView)
                {
                    Debug.LogWarning($"[GetMachineInputStall] Stalled response is unreadable (missing details or unknown reason {Stall?.Reason}); treating it as unreadable");
                    unreadableView = default;
                    return false;
                }

                #endregion
            }
        }

        #endregion
    }
}
