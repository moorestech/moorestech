using System;
using System.Collections.Generic;
using System.Linq;
using Client.Game.InGame.Context;
using Client.Network.API;
using Core.Master;
using MessagePack;
using Server.Event.EventReceive;
using UnityEngine;
using VContainer;

namespace Client.Game.InGame.UI.Challenge
{
    public class ChallengeListUI : MonoBehaviour
    {
        [SerializeField] private Transform challengeListParent;
        [SerializeField] private ChallengeListUIElement challengeListUIElementPrefab;
        
        private readonly Dictionary<Guid, ChallengeListUIElement> _challengeListUIElements = new();
        
        [Inject]
        public void Construct(InitialHandshakeResponse initial)
        {
            // UIの作成
            // Create UI
            foreach (var challenge in MasterHolder.ChallengeMaster.ChallengeMasterElements)
            {
                var guid = challenge.ChallengeGuid;
                var challengeListUIElement = Instantiate(challengeListUIElementPrefab, challengeListParent);
                challengeListUIElement.Initialize(challenge);
                
                _challengeListUIElements.Add(guid, challengeListUIElement);
            }
            
            foreach (var challengeListUIElement in _challengeListUIElements.Values)
            {
                challengeListUIElement.CreateConnect(_challengeListUIElements);
            }
            
            // チャレンジ完了時のイベント登録
            // Register event when challenge is completed
            ClientContext.VanillaApi.Event.SubscribeEventResponse(CompletedChallengeEventPacket.EventTag, OnCompletedChallenge);
            
            // チャレンジの初期状態を設定
            // Set the initial state of the challenge
            foreach (var currentChallenge in initial.Challenge.CurrentChallenges)
            {
                if (_challengeListUIElements.TryGetValue(currentChallenge.ChallengeGuid, out var challengeListUIElement))
                {
                    const bool isComplete = false;
                    challengeListUIElement.SetStatus(isComplete);
                }
            }
            foreach (var completedChallenge in initial.Challenge.CompletedChallenges)
            {
                if (_challengeListUIElements.TryGetValue(completedChallenge.ChallengeGuid, out var challengeListUIElement))
                {
                    const bool isComplete = true;
                    challengeListUIElement.SetStatus(isComplete);
                }
            }
        }
        
        /// <summary>
        /// チャレンジが完了したときにUIを更新する処理
        /// Update the UI when the challenge is completed
        /// </summary>
        private void OnCompletedChallenge(byte[] packet)
        {
            var message = MessagePackSerializer.Deserialize<CompletedChallengeEventMessagePack>(packet);
            var challengeInfo = MasterHolder.ChallengeMaster.GetChallenge(message.CompletedChallengeGuid);
            
            if (_challengeListUIElements.TryGetValue(challengeInfo.ChallengeGuid, out var challengeListUIElement))
            {
                challengeListUIElement.SetStatus(true);
            }
        }
    }
}