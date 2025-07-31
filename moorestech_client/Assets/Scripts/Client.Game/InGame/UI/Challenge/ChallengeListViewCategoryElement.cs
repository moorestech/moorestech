using System;
using Core.Master;
using Mooresmaster.Model.ChallengesModule;
using Server.Protocol.PacketResponse;
using UnityEngine;
using UnityEngine.UI;

namespace Client.Game.InGame.UI.Challenge
{
    public class ChallengeListViewCategoryElement: MonoBehaviour
    {
        [SerializeField] private Button button;
        
        private ChallengeTreeView _challengeTreeView;
        private ChallengeCategoryMasterElement _currentCategory;
        
        private void Awake()
        {
            button.onClick.AddListener(() =>
            {
                _challengeTreeView.SetChallengeCategory(_currentCategory);
            });
        }
        
        
        public void SetUI(ChallengeCategoryMasterElement category, ChallengeTreeView challengeTreeView)
        {
            _challengeTreeView = challengeTreeView;
            _currentCategory = category;
        }
    }
}