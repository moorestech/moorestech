using System;
using System.Collections.Generic;
using Game.Research;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;

namespace Server.Protocol.PacketResponse
{
    public class CompleteResearchProtocol : IPacketResponse
    {
        public const string ProtocolTag = "va:completeResearch";

        private readonly IResearchDataStore _researchDataStore;

        public CompleteResearchProtocol(ServiceProvider serviceProvider)
        {
            _researchDataStore = serviceProvider.GetService<IResearchDataStore>();
        }

        public ProtocolMessagePackBase GetResponse(List<byte> payload)
        {
            var request = MessagePackSerializer.Deserialize<RequestCompleteResearchMessagePack>(payload.ToArray());

            // 研究完了を試みる
            var isSuccess = _researchDataStore.CompleteResearch(request.ResearchGuid, request.PlayerId);
            
            return new ResponseCompleteResearchMessagePack(isSuccess, request.ResearchGuid.ToString());
        }

        #region MessagePack Classes

        [MessagePackObject]
        public class RequestCompleteResearchMessagePack : ProtocolMessagePackBase
        {
            [Key(2)] public int PlayerId { get; set; }
            [Key(3)] public string ResearchGuidStr { get; set; }
            [IgnoreMember] public Guid ResearchGuid => Guid.Parse(ResearchGuidStr);

            [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
            public RequestCompleteResearchMessagePack()
            {
            }

            public RequestCompleteResearchMessagePack(int playerId, Guid researchGuid)
            {
                Tag = ProtocolTag;
                PlayerId = playerId;
                ResearchGuidStr = researchGuid.ToString();
            }
        }

        [MessagePackObject]
        public class ResponseCompleteResearchMessagePack : ProtocolMessagePackBase
        {
            [Key(2)] public bool Success { get; set; }
            [Key(3)] public string CompletedResearchGuidStr { get; set; }

            [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
            public ResponseCompleteResearchMessagePack()
            {
            }

            public ResponseCompleteResearchMessagePack(bool success, string completedResearchGuidStr)
            {
                Tag = ProtocolTag;
                Success = success;
                CompletedResearchGuidStr = completedResearchGuidStr;
            }
        }

        #endregion
    }
}