using System.Collections.Generic;
using System.Threading;
using Client.Game.InGame.Context;
using Client.Network.API;
using Cysharp.Threading.Tasks;
using Server.Event.EventReceive;
using UnityEngine;
using VContainer;

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
                categoryElement.SetUI(category.Category, challengeTreeView);
            }
        }
        public void SetActive(bool enable)
        {
            gameObject.SetActive(enable);
        }
    }
}