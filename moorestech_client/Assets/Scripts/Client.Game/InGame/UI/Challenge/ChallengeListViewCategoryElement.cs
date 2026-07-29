// [uGUI廃止Phase1] Web UI移行済みのため未メンテ・描画恒久停止。Phase2で削除予定（docs/webui/ugui-retirement-plan.md）
// [uGUI retirement Phase1] Unmaintained; rendering permanently disabled after the Web UI migration. Slated for deletion in Phase2 (docs/webui/ugui-retirement-plan.md)
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
        public Guid CategoryGuid => _currentCategory.Category.CategoryGuid;
        
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
            
            // 現在のツリービューがこのカテゴリを表示している場合、ツリービューを更新する
            if (_challengeTreeView.CurrentCategoryGuid == categoryResponse.Category.CategoryGuid)
            {
                _challengeTreeView.SetChallengeCategory(categoryResponse);
            }
        }
    }
}