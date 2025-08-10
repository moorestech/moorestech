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
        private ChallengeListView _challengeListView;
        
        [Inject]
        public void Construct(ChallengeListView challengeListView, InitialHandshakeResponse initialHandshakeResponse)
        {
            foreach (var challengeCategory in initialHandshakeResponse.Challenges)
            {
                // チュートリアルの適用
                // Apply tutorial
                challengeCategory.CurrentChallenges.ForEach(c => _tutorialManager.ApplyTutorial(c.ChallengeGuid));
            }
            
            var currentChallenges = initialHandshakeResponse.Challenges.SelectMany(c => c.CurrentChallenges).ToList();
            currentChallengeHudView.SetCurrentChallenge(currentChallenges);
            _challengeListView = challengeListView;
            _challengeListView.SetUI(initialHandshakeResponse.Challenges);
            
            ClientContext.VanillaApi.Event.SubscribeEventResponse(CompletedChallengeEventPacket.EventTag, OnCompletedChallenge);
        }
        
        private void OnCompletedChallenge(byte[] packet)
        {
            var message = MessagePackSerializer.Deserialize<CompletedChallengeEventMessagePack>(packet);
            var nextChallenges = message.NextChallengeGuids.Select(c => MasterHolder.ChallengeMaster.GetChallenge(c)).ToList();
            
            // チャレンジリストを更新
            _challengeListView.UpdateUI(message.ChallengeCategories);
            
            // チュートリアルを完了
            _tutorialManager.CompleteChallenge(message.CompletedChallengeGuid);
            
            // 完了したチャレンジのアニメーションを再生してから次のチャレンジを表示
            ProcessChallengeCompletion(message.CompletedChallengeGuid, nextChallenges).Forget();
            
            #region Internal
            
            async UniTask ProcessChallengeCompletion(Guid completedChallengeGuid, List<ChallengeMasterElement> nextList)
            {
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
    }
}