using System.Collections.Generic;
using System.Linq;
using Game.Challenge;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using Server.Event;

namespace Server.Protocol.PacketResponse
{
    public class RegisterPlayedSkitProtocol : IPacketResponse
    {
        public const string ProtocolTag = "va:registerPlayedSkit";
        
        private readonly ChallengeDatastore _challengeDatastore;
        private readonly EventProtocolProvider _eventProtocolProvider;
        
        public RegisterPlayedSkitProtocol(ServiceProvider serviceProvider)
        {
            _challengeDatastore = serviceProvider.GetService<ChallengeDatastore>();
            _eventProtocolProvider = serviceProvider.GetService<EventProtocolProvider>();
        }
        
        public ProtocolMessagePackBase GetResponse(List<byte> payload)
        {
            var data = MessagePackSerializer.Deserialize<RegisterPlayedSkitMessagePack>(payload.ToArray());
            var info = _challengeDatastore.GetOrCreateChallengeInfo(data.PlayerId);
            
            // 既に登録されている場合は重複登録しない
            if (info.PlayedSkitIds.Contains(data.SkitId)) return null;
            
            info.PlayedSkitIds.Add(data.SkitId);
            
            // 再生済みスキットリストのイベントを送信
            var eventData = new Event.EventReceive.SkitRegisterEventPacket.SkitRegisterEventMessagePack(info.PlayedSkitIds);
            var eventPayload = MessagePackSerializer.Serialize(eventData);
            _eventProtocolProvider.AddEvent(data.PlayerId, Event.EventReceive.SkitRegisterEventPacket.EventTag, eventPayload);
            
            return null;
        }
        
        [MessagePackObject]
        public class RegisterPlayedSkitMessagePack : ProtocolMessagePackBase
        {
            [Key(2)] public int PlayerId { get; set; }
            [Key(3)] public string SkitId { get; set; }
            
            [System.Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
            public RegisterPlayedSkitMessagePack() { }
            
            public RegisterPlayedSkitMessagePack(int playerId, string skitId)
            {
                Tag = ProtocolTag;
                PlayerId = playerId;
                SkitId = skitId;
            }
        }
    }
}