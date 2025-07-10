using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Mooresmaster.Model.ChallengesModule;
using UnityEngine;

namespace Client.Game.InGame.UI.Challenge
{
    public class CurrentChallengeHudView : MonoBehaviour
    {
        [SerializeField] private CurrentChallengeHudViewElement challengeElementPrefab;
        [SerializeField] private Transform challengeElementContainer;
        
        private readonly List<CurrentChallengeHudViewElement> _currentElements = new();
        
        public void SetActive(bool active)
        {
            gameObject.SetActive(active);
        }
        
        public void SetCurrentChallenge(List<ChallengeMasterElement> nextChallenges)
        {
            if (nextChallenges.Count == 0) return;
            
            foreach (var challenge in nextChallenges)
            {
                var element = Instantiate(challengeElementPrefab, challengeElementContainer);
                element.Initialize(challenge);
                _currentElements.Add(element);
            }
        }
        
        public async UniTask OnChallengeCompleted(Guid completedChallengeGuid)
        {
            var completedElement = _currentElements.Find(e => e.ChallengeMasterElement.ChallengeGuid == completedChallengeGuid);
            if (completedElement != null)
            {
                await completedElement.OnCompleteChallenge();
                _currentElements.Remove(completedElement);
            }
        }
    }
}