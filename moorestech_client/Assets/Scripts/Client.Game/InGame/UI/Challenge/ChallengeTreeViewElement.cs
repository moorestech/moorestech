using Mooresmaster.Model.ChallengesModule;
using UnityEngine;

namespace Client.Game.InGame.UI.Challenge
{
    public class ChallengeTreeViewElement : MonoBehaviour
    {
        [SerializeField] private RectTransform rectTransform;
        
        public void SetChallenge(ChallengeMasterElement challengeMasterElement)
        {
            rectTransform.anchoredPosition = challengeMasterElement.DisplayListParam.UIPosition;
            rectTransform.localScale = challengeMasterElement.DisplayListParam.UIScale;
        }
    }
}