// [uGUI廃止Phase1] Web UI移行済みのため未メンテ・描画恒久停止。Phase2で削除予定（docs/webui/ugui-retirement-plan.md）
// [uGUI retirement Phase1] Unmaintained; rendering permanently disabled after the Web UI migration. Slated for deletion in Phase2 (docs/webui/ugui-retirement-plan.md)
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