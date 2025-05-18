using System.Collections.Generic;
using Mooresmaster.Model.ChallengesModule;
using TMPro;
using UnityEngine;

namespace Client.Game.InGame.UI.Challenge
{
    public class CurrentChallengeHudView : MonoBehaviour
    {
        [SerializeField] private TMP_Text challengeText;
        
        public void SetActive(bool active)
        {
            gameObject.SetActive(active);
        }
        
        public void SetCurrentChallenge(List<ChallengeMasterElement> nextChallenges)
        {
            if (nextChallenges.Count == 0)
            {
                challengeText.text = string.Empty;
                return;
            }
            
            var challengeTexts = string.Empty;
            foreach (var challenge in nextChallenges)
            {
                challengeTexts += $"・{challenge.Title}\n";
            }
            
            challengeText.text = challengeTexts;
        }
        
    }
}