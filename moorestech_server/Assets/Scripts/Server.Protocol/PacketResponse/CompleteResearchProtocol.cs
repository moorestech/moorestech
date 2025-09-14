using System;
using System.Collections.Generic;
using System.Linq;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;

namespace Server.Protocol.PacketResponse
{
    public class CompleteResearchProtocol : IPacketResponse
    {
        public const string ProtocolTag = "va:completeResearch";
        private readonly dynamic _research;

        public CompleteResearchProtocol(ServiceProvider sp)
        {
            var t = Type.GetType("Game.Research.Interface.IResearchDataStore, Assembly-CSharp");
            _research = sp.GetService(t);
        }

        public ProtocolMessagePackBase GetResponse(List<byte> payload)
        {
            var req = MessagePackSerializer.Deserialize<Request>(payload.ToArray());
            var guid = Guid.Parse(req.ResearchGuid);
            var result = _research.CompleteResearch(guid, req.PlayerId);
            var all = (IEnumerable<Guid>)_research.GetCompletedResearchGuids();
            return new Response
            {
                Success = result.Success,
                ResearchGuid = req.ResearchGuid,
                FailureReason = result.Reason,
                CompletedResearchGuids = all.Select(g => g.ToString()).ToList()
            };
        }

        [MessagePackObject]
        public class Request : ProtocolMessagePackBase
        {
            public Request() { Tag = ProtocolTag; }
            [Key(2)] public int PlayerId { get; set; }
            [Key(3)] public string ResearchGuid { get; set; }
        }

        [MessagePackObject]
        public class Response : ProtocolMessagePackBase
        {
            [Key(2)] public bool Success { get; set; }
            [Key(3)] public string ResearchGuid { get; set; }
            [Key(4)] public string FailureReason { get; set; }
            [Key(5)] public List<string> CompletedResearchGuids { get; set; }
        }
    }
}
