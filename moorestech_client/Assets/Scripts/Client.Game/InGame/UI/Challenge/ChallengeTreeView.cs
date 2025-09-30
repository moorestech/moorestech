using System;
using System.Collections.Generic;
using System.Linq;
using Client.Network.API;
using Mooresmaster.Model.ChallengesModule;
using UnityEngine;

namespace Client.Game.InGame.UI.Challenge
{
    public class ChallengeTreeView : MonoBehaviour
    {
        public Guid CurrentCategoryGuid { get; private set; }
        
        [SerializeField] private ChallengeTreeViewElement categoryElement;
        
        [SerializeField] private Transform challengeListParent;
        [SerializeField] private Transform connectLineParent; // 線は一番下に表示される必要があるため専用の親に格納する

        [SerializeField] private RectTransform resizeTarget;
        [SerializeField] private RectTransform offsetTarget;
        
        private readonly Dictionary<Guid, ChallengeTreeViewElement> _challengeElementsDictionary = new();
        
        public void SetChallengeCategory(ChallengeCategoryResponse categoryResponse)
        {
            CurrentCategoryGuid = categoryResponse.Category.CategoryGuid;
            
            // 既存の要素をクリア
            ClearChallengeElements();
            
            // 新しいチャレンジ要素を作成
            foreach (var challenge in categoryResponse.Category.Challenges)
            {
                var challengeElement = Instantiate(categoryElement, challengeListParent);
                var currentState = GetCurrentState(challenge);
                challengeElement.SetChallenge(challenge, currentState);
                
                _challengeElementsDictionary.Add(challenge.ChallengeGuid, challengeElement);
            }
            
            // 接続線を作成
            foreach (var challengeElement in _challengeElementsDictionary.Values)
            {
                challengeElement.CreateConnect(connectLineParent, _challengeElementsDictionary);
            }
            
            // 全要素を包含するように親のサイズを調整
            var elements = _challengeElementsDictionary.Values.Select(e => (ITreeViewElement)e);
            TreeViewAdjuster.AdjustParentSize(resizeTarget, offsetTarget, elements);
            
            #region Internal
            
            ChallengeListUIElementState GetCurrentState(ChallengeMasterElement challengeMasterElement)
            {
                if (categoryResponse.CurrentChallenges.Contains(challengeMasterElement))
                {
                    return ChallengeListUIElementState.Current;
                }
                if (categoryResponse.CompletedChallenges.Contains(challengeMasterElement))
                {
                    return ChallengeListUIElementState.Completed;
                }
                
                return ChallengeListUIElementState.Before;
            }
            
            #endregion
        }
        
        private void ClearChallengeElements()
        {
            foreach (var element in _challengeElementsDictionary.Values)
            {
                if (element != null)
                {
                    Destroy(element.gameObject);
                }
            }
            _challengeElementsDictionary.Clear();
        }
    }
}