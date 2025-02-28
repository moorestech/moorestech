using System;
using System.Collections.Generic;
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
        [SerializeField] private RectTransform scrollContent;
        [SerializeField] private Vector2 scrollContentPadding;
        
        [SerializeField] private Transform challengeListParent;
        [SerializeField] private Transform connectLineParent; // 線は一番下に表示される必要があるため専用の親に格納する
        [SerializeField] private ChallengeListUIElement challengeListUIElementPrefab;
        
        private readonly Dictionary<Guid, ChallengeListUIElement> _challengeListUIElements = new();
        
        [Inject]
        public void Construct(InitialHandshakeResponse initial)
        {
            // チャレンジ完了時のイベント登録
            // Register event when challenge is completed
            ClientContext.VanillaApi.Event.SubscribeEventResponse(CompletedChallengeEventPacket.EventTag, OnCompletedChallenge);
            
            // チャレンジUIの作成
            // Create challenge UI
            CreateChallenges();
            
            // チャレンジの初期状態を設定
            // Set the initial state of the challenge
            SetInitialChallengeState();
            
            // ScrollContentのRectTransformを設定
            // Set the RectTransform of ScrollContent
            SetScrollContentRectTransform();
            
            SetActive(false);
            
            #region Internal
            
            void CreateChallenges()
            {
                foreach (var challenge in MasterHolder.ChallengeMaster.ChallengeMasterElements)
                {
                    var guid = challenge.ChallengeGuid;
                    var challengeListUIElement = Instantiate(challengeListUIElementPrefab, challengeListParent);
                    challengeListUIElement.Initialize(challenge);
                    
                    _challengeListUIElements.Add(guid, challengeListUIElement);
                }
                
                foreach (var challengeListUIElement in _challengeListUIElements.Values)
                {
                    challengeListUIElement.CreateConnect(connectLineParent, _challengeListUIElements);
                }
            }
            
            void SetInitialChallengeState()
            {
                // 挑戦中と完了したチャレンジの状態を設定
                // Set the status of the challenges in progress and completed
                var currentOrCompleted = new HashSet<Guid>();
                foreach (var currentChallenge in initial.Challenge.CurrentChallenges)
                {
                    if (_challengeListUIElements.TryGetValue(currentChallenge.ChallengeGuid, out var challengeListUIElement))
                    {
                        challengeListUIElement.SetStatus(ChallengeListUIElementState.Current);
                        currentOrCompleted.Add(currentChallenge.ChallengeGuid);
                    }
                }
                foreach (var completedChallenge in initial.Challenge.CompletedChallenges)
                {
                    if (_challengeListUIElements.TryGetValue(completedChallenge.ChallengeGuid, out var challengeListUIElement))
                    {
                        challengeListUIElement.SetStatus(ChallengeListUIElementState.Completed);
                        currentOrCompleted.Add(completedChallenge.ChallengeGuid);
                    }
                }
                
                // 挑戦前の状態を設定
                foreach (var challengeListUIElement in _challengeListUIElements.Values)
                {
                    if (!currentOrCompleted.Contains(challengeListUIElement.ChallengeMasterElement.ChallengeGuid))
                    {
                        challengeListUIElement.SetStatus(ChallengeListUIElementState.Before);
                    }
                }
            }
            
            void SetScrollContentRectTransform()
            {
                var min = new Vector2(float.MaxValue, float.MaxValue);
                var max = new Vector2(float.MinValue, float.MinValue);
                
                foreach (var challengeListUIElement in _challengeListUIElements.Values)
                {
                    var position = challengeListUIElement.AnchoredPosition;
                    min = Vector2.Min(min, position);
                    max = Vector2.Max(max, position);
                }
                
                var size = max - min + scrollContentPadding;
                scrollContent.sizeDelta = size;
            }
            
  #endregion
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
                challengeListUIElement.SetStatus(ChallengeListUIElementState.Completed);
            }
            
            foreach (var nextChallenge in message.NextChallengeGuids)
            {
                if (_challengeListUIElements.TryGetValue(nextChallenge, out var nextChallengeListUIElement))
                {
                    nextChallengeListUIElement.SetStatus(ChallengeListUIElementState.Current);
                }
            }
            
        }
        public void SetActive(bool enable)
        {
            gameObject.SetActive(enable);
        }
    }
}