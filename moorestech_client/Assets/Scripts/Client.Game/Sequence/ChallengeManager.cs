using System;
using System.Collections.Generic;
using System.Linq;
using Client.Game.InGame.BackgroundSkit;
using Client.Game.InGame.Context;
using Client.Game.InGame.Tutorial;
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
        
        private TutorialManager _tutorialManager;
        
        [Inject]
        public void Construct(InitialHandshakeResponse initialHandshakeResponse, TutorialManager tutorialManager)
        {
            _tutorialManager = tutorialManager;
            
            //TODO 複数のチャレンジを表示する
            if (initialHandshakeResponse.Challenge.CurrentChallenges.Count != 0)
            {
                var currentChallenge = initialHandshakeResponse.Challenge.CurrentChallenges.First();
                if (currentChallenge != null) currentChallengeSummary.text = currentChallenge.Summary;
                
                ClientContext.VanillaApi.Event.RegisterEventResponse(CompletedChallengeEventPacket.EventTag, OnCompletedChallenge);
                
                // チュートリアルの適用
                // Apply tutorial
                initialHandshakeResponse.Challenge.CurrentChallenges.ForEach(c => _tutorialManager.ApplyTutorial(c.Id));
            }
        }
        
        private void OnCompletedChallenge(byte[] packet)
        {
            var message = MessagePackSerializer.Deserialize<CompletedChallengeEventMessage>(packet);
            var challengeInfo = ServerContext.ChallengeConfig.GetChallenge(message.CompletedChallengeId);
            var nextIds = challengeInfo.NextIds;
            
            // スキットの再生
            // Play background skit
            PlaySkit(nextIds).Forget();
            
            // チャレンジのテキストの更新 TODO 複数のチャレンジに対応させる
            // Update challenge text TODO Correspond to multiple challenges
            if (challengeInfo.NextIds.Count != 0)
            {
                var nextId = challengeInfo.NextIds.First();
                var nextChallenge = ServerContext.ChallengeConfig.GetChallenge(nextId);
                
                currentChallengeSummary.text = nextChallenge.Summary;
            }
            
            // チュートリアルの適用
            // Apply tutorial
            nextIds.ForEach(id => _tutorialManager.ApplyTutorial(id));
        }
        
        private async UniTask PlaySkit(List<int> nextIds)
        {
            foreach (var id in nextIds)
            {
                var challengeInfo = ServerContext.ChallengeConfig.GetChallenge(id);
                
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