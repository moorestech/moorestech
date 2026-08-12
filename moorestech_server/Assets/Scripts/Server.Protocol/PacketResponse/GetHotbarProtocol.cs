using System;
using System.Linq;
using Game.Hotbar;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;

namespace Server.Protocol.PacketResponse
{
    /// <summary>
    /// ホットバー9枠の割当一覧を取得する（前例 GetGameUnlockStateProtocol）
    /// Fetches all 9 hotbar assignment slots (precedent: GetGameUnlockStateProtocol).
    /// </summary>
    public class GetHotbarProtocol : IPacketResponse
    {
        public const string ProtocolTag = "va:getHotbar";

        private readonly HotbarAssignmentDatastore _hotbarAssignmentDatastore;

        public GetHotbarProtocol(ServiceProvider serviceProvider)
        {
            _hotbarAssignmentDatastore = serviceProvider.GetService<HotbarAssignmentDatastore>();
        }

        public ProtocolMessagePackBase GetResponse(byte[] payload, PacketResponseContext context)
        {
            var request = MessagePackSerializer.Deserialize<RequestGetHotbarMessagePack>(payload);
            var assignments = _hotbarAssignmentDatastore.GetAssignments(request.PlayerId);

            return new ResponseGetHotbarMessagePack(assignments.ToArray());
        }

        #region MessagePack

        [MessagePackObject]
        public class RequestGetHotbarMessagePack : ProtocolMessagePackBase
        {
            [Key(2)] public int PlayerId { get; set; }

            [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
            public RequestGetHotbarMessagePack() { Tag = ProtocolTag; }

            public RequestGetHotbarMessagePack(int playerId)
            {
                Tag = ProtocolTag;
                PlayerId = playerId;
            }
        }

        [MessagePackObject]
        public class ResponseGetHotbarMessagePack : ProtocolMessagePackBase
        {
            [Key(2)] public Guid[] Assignments { get; set; }

            [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
            public ResponseGetHotbarMessagePack() { }

            public ResponseGetHotbarMessagePack(Guid[] assignments)
            {
                Tag = ProtocolTag;
                Assignments = assignments;
            }
        }

        #endregion
    }
}
