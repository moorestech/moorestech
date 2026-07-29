// [uGUI廃止Phase1] uGUI描画は恒久停止・ビューは未メンテ。ただし本クラスは外部（Web UIブリッジ等）から参照中のため削除前に整理が必要（docs/webui/ugui-retirement-plan.md）
// [uGUI retirement Phase1] uGUI rendering is permanently disabled and the view is unmaintained, but this class is still referenced externally (e.g. Web UI bridge); untangle before deletion (docs/webui/ugui-retirement-plan.md)
using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Mooresmaster.Model.ChallengesModule;
using UnityEngine;
using Client.Game.InGame.UI.UIState;

namespace Client.Game.InGame.UI.Challenge
{
    public class CurrentChallengeHudView : MonoBehaviour
    {
        [SerializeField] private CurrentChallengeHudViewElement challengeElementPrefab;
        [SerializeField] private Transform challengeElementContainer;
        
        private readonly List<CurrentChallengeHudViewElement> _currentElements = new();
        
        public void SetActive(bool active)
        {
            gameObject.SetActive(active && !WebUiScreenGate.IsWebUiMode);
        }
        
        public void SetCurrentChallenge(List<ChallengeMasterElement> nextChallenges)
        {
            if (nextChallenges.Count == 0) return;
            
            foreach (var challenge in nextChallenges)
            {
                var element = Instantiate(challengeElementPrefab, challengeElementContainer);
                element.Initialize(challenge);
                _currentElements.Add(element);
            }
        }
        
        public async UniTask OnChallengeCompleted(Guid completedChallengeGuid)
        {
            var completedElement = _currentElements.Find(e => e.ChallengeMasterElement.ChallengeGuid == completedChallengeGuid);
            if (completedElement != null)
            {
                await completedElement.OnCompleteChallenge();
                _currentElements.Remove(completedElement);
            }
        }
    }
}
