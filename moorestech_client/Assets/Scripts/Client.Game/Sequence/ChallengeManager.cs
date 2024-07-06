using System;
using System.Collections.Generic;
using System.Linq;
using Client.Game.InGame.BackgroundSkit;
using Client.Game.InGame.Context;
using Client.Network.API;
using Cysharp.Threading.Tasks;
using Game.Challenge;
using Game.Context;
using MessagePack;
using Server.Event.EventReceive;
using TMPro;
using UnityEngine;
using VContainer;

namespace Client.Game.Sequence
{
    public class ChallengeManager : MonoBehaviour
    {
        [SerializeField] private TMP_Text currentChallengeSummary;
        [SerializeField] private BackgroundSkitManager backgroundSkitManager;
        
        [SerializeField] private List<ChallengeTextAsset> challengeTextAssets;
        
        private ChallengeConfig _challengeConfig;
        
        [Inject]
        public void Construct(InitialHandshakeResponse initialHandshakeResponse)
        {
            _challengeConfig = ServerContext.GetService<ChallengeConfig>();
            if (initialHandshakeResponse.Challenge.CurrentChallenges.Count != 0)
            {
                var currentChallenge = initialHandshakeResponse.Challenge.CurrentChallenges.First();
                if (currentChallenge != null) currentChallengeSummary.text = currentChallenge.Summary;
                
                ClientContext.VanillaApi.Event.RegisterEventResponse(CompletedChallengeEventPacket.EventTag, OnCompletedChallenge);
            }
        }
        
        private void OnCompletedChallenge(byte[] packet)
        {
            var message = MessagePackSerializer.Deserialize<CompletedChallengeEventMessage>(packet);
            var challengeInfo = _challengeConfig.GetChallenge(message.CompletedChallengeId);
            var nextIds = challengeInfo.NextIds;
            
            PlaySkit(nextIds).Forget();
            
            if (challengeInfo.NextIds.Count != 0)
            {
                var nextId = challengeInfo.NextIds.First();
                var nextChallenge = _challengeConfig.GetChallenge(nextId);
                
                currentChallengeSummary.text = nextChallenge.Summary;
            }
        }
        
        private async UniTask PlaySkit(List<int> nextIds)
        {
            foreach (var id in nextIds)
            {
                var challengeInfo = _challengeConfig.GetChallenge(id);
                
                if (challengeInfo.FireSkitType == ChallengeInfo.BackgroundSkitType)
                {
                    var challengeTextAsset = challengeTextAssets.FirstOrDefault(x => x.SkitName == challengeInfo.FireSkitName);
                    if (challengeTextAsset == null) continue;
                    
                    await backgroundSkitManager.StartBackgroundSkit(challengeTextAsset.TextAsset);
                }
            }
        }
    }
    
    [Serializable]
    public class ChallengeTextAsset
    {
        public string SkitName;
        public TextAsset TextAsset;
    }
}