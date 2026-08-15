using System;
using Game.Hotbar;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;

namespace Server.Protocol.PacketResponse
{
    /// <summary>
    /// ホットバー割当のAssign/Clear/Swapを扱う。結果は直接応答せずイベントパケットで通知する
    /// Handles Assign/Clear/Swap of hotbar assignments; results arrive via the event packet, not a direct response.
    /// </summary>
    public class HotbarProtocol : IPacketResponse
    {
        public const string ProtocolTag = "va:hotbar";

        private readonly IHotbarAssignmentMutation _hotbarAssignmentMutation;

        public HotbarProtocol(ServiceProvider serviceProvider)
        {
            _hotbarAssignmentMutation = serviceProvider.GetService<IHotbarAssignmentMutation>();
        }

        public ProtocolMessagePackBase GetResponse(byte[] payload, PacketResponseContext context)
        {
            var request = MessagePackSerializer.Deserialize<HotbarProtocolMessagePack>(payload);

            // 範囲外slot・未解決Guidの検証はdatastore側で完結しているためここで二重に行わない
            // Out-of-range slots and unresolvable ids are already validated inside the datastore; no need to re-check here
            switch (request.Operation)
            {
                case HotbarOperation.Assign:
                    _hotbarAssignmentMutation.SetAssignment(request.PlayerId, request.Slot, request.TargetId);
                    break;
                case HotbarOperation.Clear:
                    _hotbarAssignmentMutation.ClearAssignment(request.PlayerId, request.Slot);
                    break;
                case HotbarOperation.Swap:
                    _hotbarAssignmentMutation.SwapAssignments(request.PlayerId, request.Slot, request.SlotB);
                    break;
            }

            return null;
        }

        #region MessagePack

        [MessagePackObject]
        public class HotbarProtocolMessagePack : ProtocolMessagePackBase
        {
            [Key(2)] public int PlayerId { get; set; }
            [Key(3)] public HotbarOperation Operation { get; set; }
            [Key(4)] public int Slot { get; set; }
            [Key(5)] public Guid TargetId { get; set; }
            [Key(6)] public int SlotB { get; set; }

            [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
            public HotbarProtocolMessagePack() { Tag = ProtocolTag; }

            // Operationごとに必要フィールドのみ設定
            // Private constructor; static factories below set only the fields each Operation needs
            private HotbarProtocolMessagePack(int playerId, HotbarOperation operation, int slot, Guid targetId, int slotB)
            {
                Tag = ProtocolTag;
                PlayerId = playerId;
                Operation = operation;
                Slot = slot;
                TargetId = targetId;
                SlotB = slotB;
            }

            public static HotbarProtocolMessagePack CreateAssignRequest(int playerId, int slot, Guid targetId)
            {
                return new HotbarProtocolMessagePack(playerId, HotbarOperation.Assign, slot, targetId, 0);
            }

            public static HotbarProtocolMessagePack CreateClearRequest(int playerId, int slot)
            {
                return new HotbarProtocolMessagePack(playerId, HotbarOperation.Clear, slot, Guid.Empty, 0);
            }

            public static HotbarProtocolMessagePack CreateSwapRequest(int playerId, int slotA, int slotB)
            {
                return new HotbarProtocolMessagePack(playerId, HotbarOperation.Swap, slotA, Guid.Empty, slotB);
            }
        }

        #endregion
    }

    public enum HotbarOperation
    {
        Assign = 0,
        Clear = 1,
        Swap = 2,
    }
}
