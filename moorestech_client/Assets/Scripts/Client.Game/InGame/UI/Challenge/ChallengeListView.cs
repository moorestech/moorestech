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
        
        private List<ChallengeListViewCategoryElement> _categoryListElements = new();
        
        
        public void SetUI(List<ChallengeCategoryResponse> challengeCategories)
        {
            // categoryListParent内の全ての子要素を削除
            foreach (var child in _categoryListElements)
            {
                Destroy(child.gameObject);
            }
            _categoryListElements.Clear();
            
            // アンロックされたカテゴリを処理
            foreach (var category in challengeCategories)
            {
                if (!category.IsUnlocked) continue;
                
                var categoryElement = Instantiate(categoryListElementPrefab, categoryListParent);
                categoryElement.SetUI(category, challengeTreeView);
                _categoryListElements.Add(categoryElement);
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
        
#if UNITY_EDITOR
        public RectTransform DebugCategoryListParent => categoryListParent;
#endif
    }
}