using System;
using System.Collections.Generic;
using System.Linq;
using Client.Game.InGame.BackgroundSkit;
using Client.Game.InGame.Context;
using Client.Game.InGame.Tutorial;
using Client.Network.API;
using Core.Master;
using Cysharp.Threading.Tasks;
using MessagePack;
using Mooresmaster.Model.ChallengesModule;
using Server.Event.EventReceive;
using UnityEngine;
using VContainer;

namespace Client.Game.InGame.UI.Challenge
{
    public class ChallengeManager : MonoBehaviour
    {
        [SerializeField] private CurrentChallengeHudView currentChallengeHudView;
        [SerializeField] private BackgroundSkitManager backgroundSkitManager;
        
        [SerializeField] private List<ChallengeTextAsset> challengeTextAssets;
        
        [Inject] private TutorialManager _tutorialManager;
        
        [Inject]
        public void Construct(InitialHandshakeResponse initialHandshakeResponse)
        {
            //TODO 複数のチャレンジを表示する
            if (initialHandshakeResponse.Challenge.CurrentChallenges.Count != 0)
            {
                var currentChallenges = initialHandshakeResponse.Challenge.CurrentChallenges;
                currentChallengeHudView.SetCurrentChallenge(currentChallenges);
                
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
            var nextChallenges = message.NextChallengeGuids.Select(c => MasterHolder.ChallengeMaster.GetChallenge(c)).ToList();
            
            // チュートリアルを完了
            _tutorialManager.CompleteChallenge(message.CompletedChallengeGuid);
            
            // スキットの再生
            // Play background skit
            PlaySkit(nextChallenges).Forget();
            
            // チャレンジのテキストの更新 TODO 複数のチャレンジに対応させる
            // Update challenge text TODO Correspond to multiple challenges
            if (nextChallenges.Count != 0)
            {
                currentChallengeHudView.SetCurrentChallenge(nextChallenges);
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