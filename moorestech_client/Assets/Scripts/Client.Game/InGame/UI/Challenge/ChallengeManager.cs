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
            var nextChallenges = message.NextChallengeGuids.Select(c => MasterHolder.ChallengeMaster.GetChallenge(c)).ToList();
            
            // チュートリアルを完了
            _tutorialManager.CompleteChallenge(message.CompletedChallengeGuid);
            
            // 完了したチャレンジのアニメーションを再生してから次のチャレンジを表示
            ProcessChallengeCompletion(message.CompletedChallengeGuid, nextChallenges).Forget();
            
            #region Internal
            
            async UniTask ProcessChallengeCompletion(Guid completedChallengeGuid, List<ChallengeMasterElement> nextList)
            {
                // スキットの再生
                // Play background skit
                await PlaySkit(nextList);
                
                // チャレンジのテキストの更新
                // Update challenge text
                if (nextList.Count != 0)
                {
                    currentChallengeHudView.SetCurrentChallenge(nextList);
                }
                
                // チュートリアルの適用
                // Apply tutorial
                nextList.ForEach(id => _tutorialManager.ApplyTutorial(id.ChallengeGuid));
                
                // 完了したチャレンジのアニメーションを再生
                await currentChallengeHudView.OnChallengeCompleted(completedChallengeGuid);
            }
            
            #endregion
        }
        
        
        private async UniTask PlaySkit(List<ChallengeMasterElement> nextChallenges)
        {
            foreach (var challenge in nextChallenges)
            {
                if (challenge.PlaySkitType == "BackgroundSkit") // TODO いい感じの位置に置きたい
                {
                    //await backgroundSkitManager.StartBackgroundSkit(challengeTextAsset.TextAsset);
                }
            }
        }
    }
}