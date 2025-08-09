using System;
using Client.Game.InGame.Context;
using Client.Game.InGame.UI.Inventory.Common;
using Client.Network.API;
using Core.Master;
using Cysharp.Threading.Tasks;
using Mooresmaster.Model.ChallengesModule;
using Server.Protocol.PacketResponse;
using UniRx;
using UnityEngine;
using UnityEngine.UI;

namespace Client.Game.InGame.UI.Challenge
{
    public class ChallengeListViewCategoryElement: MonoBehaviour
    {
        [SerializeField] private ItemSlotView itemSlotView;
        
        private ChallengeTreeView _challengeTreeView;
        private ChallengeCategoryResponse _currentCategory;
        
        private void Awake()
        {
            itemSlotView.OnLeftClickUp.Subscribe(_ =>
            {
                _challengeTreeView.SetChallengeCategory(_currentCategory);
            }).AddTo(this);
        }
        
        
        public void SetUI(ChallengeCategoryResponse categoryResponse, ChallengeTreeView challengeTreeView)
        {
            _challengeTreeView = challengeTreeView;
            _currentCategory = categoryResponse;
            
            var itemView = ClientContext.ItemImageContainer.GetItemView(categoryResponse.Category.IconItem);
            itemSlotView.SetItem(itemView, 0, categoryResponse.Category.CategoryName);
        }
    }
}