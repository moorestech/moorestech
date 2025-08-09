using System;
using System.Collections.Generic;
using Client.Network.API;
using Core.Master;
using Mooresmaster.Model.ChallengesModule;
using Server.Event.EventReceive;
using UnityEngine;

namespace Client.Game.InGame.UI.Challenge
{
    public class ChallengeListView : MonoBehaviour
    {
        [SerializeField] private RectTransform categoryListParent;
        [SerializeField] private ChallengeTreeView challengeTreeView;
        
        [SerializeField] private ChallengeListViewCategoryElement categoryListElementPrefab;
        
        
        public void SetUI(List<ChallengeCategoryResponse> challengeCategories)
        {
            foreach (var category in challengeCategories)
            {
                if (!category.IsUnlocked) continue;
                
                var categoryElement = Instantiate(categoryListElementPrefab, categoryListParent);
                categoryElement.SetUI(category, challengeTreeView);
            }
        }
        
        
        /// <summary>
        /// challengeCategoriesを変換してSetUIを叩く
        /// </summary>
        public void UpdateUI(List<ChallengeCategoryMessagePack> challengeCategories)
        {
            var categories = new List<ChallengeCategoryResponse>();
            foreach (var category in challengeCategories)
            {
                var currentChallenges = category.CurrentChallengeGuidsStr.ConvertAll(e => MasterHolder.ChallengeMaster.GetChallenge(Guid.Parse(e)));
                var completedChallenges = category.CompletedChallengeGuidsStr.ConvertAll(e => MasterHolder.ChallengeMaster.GetChallenge(Guid.Parse(e)));
                var categoryMaster =  MasterHolder.ChallengeMaster.GetChallengeCategory(category.ChallengeCategoryGuid);
                categories.Add(new ChallengeCategoryResponse(categoryMaster, category.IsUnlocked, currentChallenges, completedChallenges));
            }
            SetUI(categories);
        }
        
        
        public void SetActive(bool enable)
        {
            gameObject.SetActive(enable);
        }
    }
}