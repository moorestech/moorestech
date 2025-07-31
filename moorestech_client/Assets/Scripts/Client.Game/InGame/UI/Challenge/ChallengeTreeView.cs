using System;
using System.Collections.Generic;
using Mooresmaster.Model.ChallengesModule;
using UnityEngine;

namespace Client.Game.InGame.UI.Challenge
{
    public class ChallengeTreeView : MonoBehaviour
    {
        [SerializeField] private ChallengeTreeViewElement categoryElement;
        
        [SerializeField] private Transform challengeListParent;
        [SerializeField] private Transform connectLineParent; // 線は一番下に表示される必要があるため専用の親に格納する
        
        private readonly Dictionary<Guid, ChallengeTreeViewElement> _challengeElementsDictionary = new();
        
        public void SetChallengeCategory(ChallengeCategoryMasterElement category)
        {
            // 既存の要素をクリア
            ClearChallengeElements();
            
            // 新しいチャレンジ要素を作成
            foreach (var challenge in category.Challenges)
            {
                var challengeElement = Instantiate(categoryElement, challengeListParent);
                challengeElement.SetChallenge(challenge);
                
                _challengeElementsDictionary.Add(challenge.ChallengeGuid, challengeElement);
            }
            
            // 接続線を作成
            foreach (var challengeElement in _challengeElementsDictionary.Values)
            {
                challengeElement.CreateConnect(connectLineParent, _challengeElementsDictionary);
            }
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