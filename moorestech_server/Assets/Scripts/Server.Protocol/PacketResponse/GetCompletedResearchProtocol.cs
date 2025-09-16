using System;
using System.Collections.Generic;
using System.Linq;
using Game.Research;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;

namespace Server.Protocol.PacketResponse
{
    public class GetCompletedResearchProtocol : IPacketResponse
    {
        public const string ProtocolTag = "va:getCompletedResearch";
        
        private readonly IResearchDataStore _researchDataStore;
        
        public GetCompletedResearchProtocol(ServiceProvider serviceProvider)
        {
            _researchDataStore = serviceProvider.GetService<IResearchDataStore>();
        }
        
        public ProtocolMessagePackBase GetResponse(List<byte> payload)
        {
            var completedNodes = _researchDataStore.GetCompletedResearchNodes();
            return new ResponseGetCompletedResearchMessagePack(completedNodes);
        }
        
        #region MessagePack Classes
        
        [MessagePackObject]
        public class RequestGetCompletedResearchMessagePack : ProtocolMessagePackBase
        {
            public RequestGetCompletedResearchMessagePack()
            {
                Tag = ProtocolTag;
            }
        }
        
        [MessagePackObject]
        public class ResponseGetCompletedResearchMessagePack : ProtocolMessagePackBase
        {
            [IgnoreMember] public IReadOnlyList<Guid> CompletedResearchGuids => CompletedResearchGuidStrings.Select(Guid.Parse).ToList();
            
            [Key(2)] public List<string> CompletedResearchGuidStrings { get; set; }
            
            [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
            public ResponseGetCompletedResearchMessagePack()
            {
            }
            
            public ResponseGetCompletedResearchMessagePack(IEnumerable<Guid> completedResearchGuids)
            {
                Tag = ProtocolTag;
                CompletedResearchGuidStrings = completedResearchGuids.Select(guid => guid.ToString()).ToList();
            }
        }
        
        #endregion
    }
}