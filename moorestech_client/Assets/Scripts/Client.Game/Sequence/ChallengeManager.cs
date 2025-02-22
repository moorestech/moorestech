using System;
using System.Collections.Generic;
using System.Linq;
using Client.Game.InGame.BackgroundSkit;
using Client.Game.InGame.Context;
using Client.Game.InGame.Tutorial;
using Client.Network.API;
using Core.Master;
using Cysharp.Threading.Tasks;
using Game.Challenge;
using Game.Context;
using MessagePack;
using Mooresmaster.Model.ChallengesModule;
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
        
        [Inject] private TutorialManager _tutorialManager;
        
        public void Construct(InitialHandshakeResponse initialHandshakeResponse)
        {
            //TODO 複数のチャレンジを表示する
            if (initialHandshakeResponse.Challenge.CurrentChallenges.Count != 0)
            {
                var currentChallenge = initialHandshakeResponse.Challenge.CurrentChallenges.First();
                if (currentChallenge != null) currentChallengeSummary.text = currentChallenge.Summary;
                
                ClientContext.VanillaApi.Event.SubscribeEventResponse(CompletedChallengeEventPacket.EventTag, OnCompletedChallenge);
                
                // チュートリアルの適用
                // Apply tutorial
                initialHandshakeResponse.Challenge.CurrentChallenges.ForEach(c => _tutorialManager.ApplyTutorial(c.ChallengeGuid));
            }
        }
        
        private void OnCompletedChallenge(byte[] packet)
        {
            var message = MessagePackSerializer.Deserialize<CompletedChallengeEventMessagePack>(packet);
            var challengeInfo = MasterHolder.ChallengeMaster.GetChallenge(message.CompletedChallengeGuid);
            var nextChallenges = MasterHolder.ChallengeMaster.GetNextChallenges(challengeInfo.ChallengeGuid);
            
            // チュートリアルを完了
            _tutorialManager.CompleteChallenge(message.CompletedChallengeGuid);
            
            // スキットの再生
            // Play background skit
            PlaySkit(nextChallenges).Forget();
            
            // チャレンジのテキストの更新 TODO 複数のチャレンジに対応させる
            // Update challenge text TODO Correspond to multiple challenges
            if (nextChallenges.Count != 0)
            {
                var nextChallenge = nextChallenges.First();
                currentChallengeSummary.text = nextChallenge.Summary;
            }
            
            // チュートリアルの適用
            // Apply tutorial
            nextChallenges.ForEach(id => _tutorialManager.ApplyTutorial(id.ChallengeGuid));
        }
        
        private async UniTask PlaySkit(List<ChallengeMasterElement> nextChallenges)
        {
            foreach (var challenge in nextChallenges)
            {
                if (challenge.PlaySkitType == "BackgroundSkit") // TODO いい感じの位置に置きたい
                {
                    var skitParam = (BackgroundSkitPlaySkitParam) challenge.PlaySkitParam;
                    var challengeTextAsset = challengeTextAssets.FirstOrDefault(x => x.SkitName == skitParam.FireSkitName);
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