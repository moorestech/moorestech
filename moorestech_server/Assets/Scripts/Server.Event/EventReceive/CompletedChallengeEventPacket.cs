using System;
using System.Collections.Generic;
using System.Linq;
using Core.Master;
using Game.Challenge;
using Game.Challenge.Task;
using Game.UnlockState;
using MessagePack;
using Newtonsoft.Json;
using UniRx;

namespace Server.Event.EventReceive
{
    public class CompletedChallengeEventPacket
    {
        public const string EventTag = "va:event:completedChallenge";
        
        private readonly EventProtocolProvider _eventProtocolProvider;
        
        private readonly IGameUnlockStateDataController _gameUnlockStateDataController;
        private readonly ChallengeDatastore _challengeDatastore;
        
        public CompletedChallengeEventPacket(EventProtocolProvider eventProtocolProvider, ChallengeEvent challengeEvent, IGameUnlockStateDataController gameUnlockStateDataController, ChallengeDatastore challengeDatastore)
        {
            _eventProtocolProvider = eventProtocolProvider;
            _gameUnlockStateDataController = gameUnlockStateDataController;
            _challengeDatastore = challengeDatastore;
            challengeEvent.OnCompleteChallenge.Subscribe(OnCompletedChallenge);
        }
        
        private void OnCompletedChallenge(ChallengeEvent.CompleteChallengeEventProperty completeProperty)
        {
            var challengeCategories = GetChallengeCategories(_challengeDatastore, _gameUnlockStateDataController);

            var messagePack = new CompletedChallengeEventMessagePack(completeProperty, challengeCategories);
            var payload = MessagePackSerializer.Serialize(messagePack);
            
            _eventProtocolProvider.AddBroadcastEvent(EventTag, payload);
        }
        
        public static List<ChallengeCategoryMessagePack> GetChallengeCategories(ChallengeDatastore challengeDatastore, IGameUnlockStateDataController gameUnlockStateDataController)
        {
            var challengeCategories = new Dictionary<Guid, ChallengeCategoryMessagePack>();
            foreach (var challengeCategory in MasterHolder.ChallengeMaster.ChallengeCategoryMasterElements)
            {
                var messagePack = new ChallengeCategoryMessagePack();
                messagePack.ChallengeCategoryGuid = challengeCategory.CategoryGuid;
                messagePack.IsUnlocked = gameUnlockStateDataController.ChallengeCategoryUnlockStateInfos[challengeCategory.CategoryGuid].IsUnlocked;
                messagePack.CurrentChallengeGuidsStr = new List<string>();
                messagePack.CompletedChallengeGuidsStr = new List<string>();
                challengeCategories.Add(challengeCategory.CategoryGuid, messagePack);
            }
            
            foreach (var currentChallenge in challengeDatastore.CurrentChallengeInfo.CurrentChallenges)
            {
                var category = MasterHolder.ChallengeMaster.GetChallengeCategoryFromChallengeGuid(currentChallenge.ChallengeMasterElement.ChallengeGuid);             
                challengeCategories[category.CategoryGuid].CurrentChallengeGuidsStr.Add(currentChallenge.ChallengeMasterElement.ChallengeGuid.ToString());
            }
            foreach (var completedChallenge in challengeDatastore.CurrentChallengeInfo.CompletedChallenges)
            {
                var category = MasterHolder.ChallengeMaster.GetChallengeCategoryFromChallengeGuid(completedChallenge.ChallengeGuid);
                challengeCategories[category.CategoryGuid].CompletedChallengeGuidsStr.Add(completedChallenge.ChallengeGuid.ToString());
            }

            return challengeCategories.Values.ToList();
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
        
        public CompletedChallengeEventMessagePack(ChallengeEvent.CompleteChallengeEventProperty completeProperty, List<ChallengeCategoryMessagePack> challengeCategories)
        {
            CompletedChallengeGuidStr = completeProperty.ChallengeTask.ChallengeMasterElement.ChallengeGuid.ToString();
            NextChallengeGuidsStr = completeProperty.NextChallengeMasterElements.ConvertAll(e => e.ChallengeGuid.ToString());
            PlayedSkitIds = completeProperty.PlayedSkitIdsStr;
            ChallengeCategories = challengeCategories;
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