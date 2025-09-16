using System;
using System.Collections.Generic;
using System.Linq;
using Game.Research;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;

namespace Server.Protocol.PacketResponse
{
    public class GetResearchInfoProtocol : IPacketResponse
    {
        public const string ProtocolTag = "va:getResearchInfo";
        
        private readonly IResearchDataStore _researchDataStore;
        
        public GetResearchInfoProtocol(ServiceProvider serviceProvider)
        {
            _researchDataStore = serviceProvider.GetService<IResearchDataStore>();
        }
        
        public ProtocolMessagePackBase GetResponse(List<byte> payload)
        {
            var request = MessagePackSerializer.Deserialize<RequestResearchInfoMessagePack>(payload.ToArray());

            var nodeStates = _researchDataStore.GetResearchNodeStates(request.PlayerId);
            return new ResponseResearchInfoMessagePack(nodeStates);
        }
        
        #region MessagePack Classes
        
        [MessagePackObject]
        public class RequestResearchInfoMessagePack : ProtocolMessagePackBase
        {
            [Key(2)] public int PlayerId { get; set; }

            public RequestResearchInfoMessagePack()
            {
                Tag = ProtocolTag;
            }

            public RequestResearchInfoMessagePack(int playerId)
            {
                Tag = ProtocolTag;
                PlayerId = playerId;
            }
        }

        [MessagePackObject]
        public class ResponseResearchInfoMessagePack : ProtocolMessagePackBase
        {
            [Key(2)] public List<ResearchNodeStateMessagePack> ResearchNodeStates { get; set; }
            
            [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
            public ResponseResearchInfoMessagePack()
            {
            }
            
            public ResponseResearchInfoMessagePack(Dictionary<Guid, ResearchNodeState> nodeStates)
            {
                Tag = ProtocolTag;
                ResearchNodeStates = nodeStates
                    .Select(kvp => new ResearchNodeStateMessagePack(kvp.Key, kvp.Value))
                    .ToList();
            }
        }

        [MessagePackObject]
        public class ResearchNodeStateMessagePack
        {
            [Key(0)] public string ResearchGuidStr { get; set; }
            [Key(1)] public ResearchNodeState State { get; set; }

            [IgnoreMember] public Guid ResearchGuid => Guid.Parse(ResearchGuidStr);

            [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
            public ResearchNodeStateMessagePack()
            {
            }

            public ResearchNodeStateMessagePack(Guid researchGuid, ResearchNodeState state)
            {
                ResearchGuidStr = researchGuid.ToString();
                State = state;
            }
        }
        
        #endregion
    }
}
