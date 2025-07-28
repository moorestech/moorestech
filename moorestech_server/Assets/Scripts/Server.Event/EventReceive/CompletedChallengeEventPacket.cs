using System;
using System.Collections.Generic;
using System.Linq;
using Game.Challenge;
using Game.Challenge.Task;
using MessagePack;
using Newtonsoft.Json;
using UniRx;

namespace Server.Event.EventReceive
{
    public class CompletedChallengeEventPacket
    {
        public const string EventTag = "va:event:completedChallenge";
        
        private readonly EventProtocolProvider _eventProtocolProvider;
        
        public CompletedChallengeEventPacket(EventProtocolProvider eventProtocolProvider, ChallengeEvent challengeEvent)
        {
            _eventProtocolProvider = eventProtocolProvider;
            challengeEvent.OnCompleteChallenge.Subscribe(OnCompletedChallenge);
        }
        
        private void OnCompletedChallenge(ChallengeEvent.CompleteChallengeEventProperty completeProperty)
        {
            var messagePack = new CompletedChallengeEventMessagePack(completeProperty);
            var payload = MessagePackSerializer.Serialize(messagePack);
            
            _eventProtocolProvider.AddBroadcastEvent(EventTag, payload);
        }
    }
    
    [MessagePackObject]
    public class CompletedChallengeEventMessagePack
    {
        [Key(0)] public string CompletedChallengeGuidStr { get; set; }
        [Key(1)] public List<string> NextChallengeGuidsStr { get; set; }
        [Key(2)] public List<string> PlayedSkitIds { get; set; }
        [Key(3)] public List<ChallengeCategoryMessagePack> ChallengeCategories { get; set; }
        
        [IgnoreMember] public Guid CompletedChallengeGuid => Guid.Parse(CompletedChallengeGuidStr);
        [IgnoreMember] public List<Guid> NextChallengeGuids => NextChallengeGuidsStr.ConvertAll(Guid.Parse);
        
        [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
        public CompletedChallengeEventMessagePack()
        {
        }
        
        public CompletedChallengeEventMessagePack(ChallengeEvent.CompleteChallengeEventProperty completeProperty)
        {
            CompletedChallengeGuidStr = completeProperty.ChallengeTask.ChallengeMasterElement.ChallengeGuid.ToString();
            NextChallengeGuidsStr = completeProperty.NextChallengeMasterElements.ConvertAll(e => e.ChallengeGuid.ToString());
            PlayedSkitIds = completeProperty.PlayedSkitIdsStr;
        }
    }
    
    
    
    [MessagePackObject]
    public class ChallengeCategoryMessagePack
    {
        [Key(0)] public Guid ChallengeCategoryGuid { get; set; }
        [Key(1)] public bool IsUnlocked { get; set; }
        
        [Key(3)] public List<string> CurrentChallengeGuidsStr { get; set; }
        [Key(4)] public List<string> CompletedChallengeGuidsStr { get; set; }
        
        [IgnoreMember] public List<Guid> CurrentChallengeGuids => CurrentChallengeGuidsStr.Select(Guid.Parse).ToList();
        [IgnoreMember] public List<Guid> CompletedChallengeGuids => CompletedChallengeGuidsStr.Select(Guid.Parse).ToList();
    }
}