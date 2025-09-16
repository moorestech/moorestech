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
            var completedNodes = _researchDataStore.GetCompletedResearchNodes();
            return new ResponseResearchInfoMessagePack(completedNodes);
        }
        
        #region MessagePack Classes
        
        [MessagePackObject]
        public class RequestResearchInfoMessagePack : ProtocolMessagePackBase
        {
            public RequestResearchInfoMessagePack()
            {
                Tag = ProtocolTag;
            }
        }
        
        [MessagePackObject]
        public class ResponseResearchInfoMessagePack : ProtocolMessagePackBase
        {
            [IgnoreMember] public IReadOnlyList<Guid> CompletedResearchGuids => CompletedResearchGuidStrings.Select(Guid.Parse).ToList();
            
            [Key(2)] public List<string> CompletedResearchGuidStrings { get; set; }
            
            [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
            public ResponseResearchInfoMessagePack()
            {
            }
            
            public ResponseResearchInfoMessagePack(IEnumerable<Guid> completedResearchGuids)
            {
                Tag = ProtocolTag;
                CompletedResearchGuidStrings = completedResearchGuids.Select(guid => guid.ToString()).ToList();
            }
        }
        
        #endregion
    }
}