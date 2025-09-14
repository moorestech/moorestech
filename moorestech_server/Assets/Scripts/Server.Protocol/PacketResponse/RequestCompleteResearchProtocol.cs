using System;
using System.Collections.Generic;
using System.Linq;
using Game.Research.Interface;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using Server.Util.MessagePack;

namespace Server.Protocol.PacketResponse
{
    public class RequestCompleteResearchProtocol : IPacketResponse
    {
        public const string ProtocolTag = "va:request_complete_research";

        private readonly IResearchDataStore _researchDataStore;

        public RequestCompleteResearchProtocol(ServiceProvider serviceProvider)
        {
            _researchDataStore = serviceProvider.GetService<IResearchDataStore>();
        }

        public ProtocolMessagePackBase GetResponse(List<byte> payload)
        {
            var request = MessagePackSerializer.Deserialize<RequestCompleteResearchMessagePack>(payload.ToArray());

            // 研究完了を試みる
            var result = _researchDataStore.CompleteResearch(request.ResearchGuid, request.PlayerId);

            if (result.Success)
            {
                return new ResponseCompleteResearchMessagePack(
                    true,
                    result.CompletedResearchGuid.ToString(),
                    null
                );
            }
            else
            {
                return new ResponseCompleteResearchMessagePack(
                    false,
                    null,
                    result.Reason
                );
            }
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
            [Key(4)] public string ErrorMessage { get; set; }

            [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
            public ResponseCompleteResearchMessagePack()
            {
            }

            public ResponseCompleteResearchMessagePack(bool success, string completedResearchGuidStr, string errorMessage)
            {
                Tag = ProtocolTag;
                Success = success;
                CompletedResearchGuidStr = completedResearchGuidStr;
                ErrorMessage = errorMessage;
            }
        }

        #endregion
    }
}