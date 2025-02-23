using System;
using System.Collections.Generic;
using Core.Master;
using UnityEngine;

namespace Client.Game.InGame.UI.Challenge
{
    public class ChallengeListUI : MonoBehaviour
    {
        [SerializeField] private Transform challengeListParent;
        [SerializeField] private ChallengeListUIElement challengeListUIElementPrefab;
        
        private readonly Dictionary<Guid, ChallengeListUIElement> _challengeListUIElements = new();
        
        public void CreateUI()
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
                challengeListUIElement.CreateConnect(_challengeListUIElements);
            }
        }
    }
}