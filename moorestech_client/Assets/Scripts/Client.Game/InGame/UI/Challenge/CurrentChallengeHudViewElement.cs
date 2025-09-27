using Cysharp.Threading.Tasks;
using Mooresmaster.Model.ChallengesModule;
using TMPro;
using UnityEngine;

namespace Client.Game.InGame.UI.Challenge
{
    public class CurrentChallengeHudViewElement : MonoBehaviour
    {
        [SerializeField] private TMP_Text challengeTextTitle;
        [SerializeField] private Animator animator;
        [SerializeField] private float completeAnimationDuration;
        public const string ChallengeCompleteAnimationName = "ChallengeComplete";
        
        public ChallengeMasterElement ChallengeMasterElement { get; private set; }
        
        public void Initialize(ChallengeMasterElement challengeMaster)
        {
            ChallengeMasterElement = challengeMaster;
            challengeTextTitle.text = $"・{challengeMaster.Title}";
        }
        
        public async UniTask OnCompleteChallenge()
        {
            if (animator != null)
            {
                animator.Play(ChallengeCompleteAnimationName);
                await UniTask.Delay((int)(completeAnimationDuration * 1000));
            }
            
            // 最後にオブジェクトをDestroyする
            Destroy(gameObject);
        }
    }
}