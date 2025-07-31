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
        
        private readonly List<ChallengeTreeViewElement> _challengeElements = new();
        
        public void SetChallengeCategory(ChallengeCategoryMasterElement category)
        {
            foreach (var challenge in category.Challenges)
            {
                var challengeElement = Instantiate(categoryElement, challengeListParent);
                challengeElement.SetChallenge(challenge);
                
                _challengeElements.Add(challengeElement);
            }
        }
    }
}