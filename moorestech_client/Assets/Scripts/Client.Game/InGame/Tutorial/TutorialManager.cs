using System;
using System.Collections.Generic;
using Core.Master;
using Game.Context;
using Mooresmaster.Model.ChallengesModule;
using Client.Game.InGame.UI.UIState;

namespace Client.Game.InGame.Tutorial
{
    public class TutorialManager
    {
        private readonly Dictionary<Guid, List<ITutorialView>> _tutorialViews = new();
        private readonly Dictionary<string, ITutorialViewManager> _tutorialViewManagers = new();

        // 起動中に完了したチャレンジへ初期適用が後から届くため、完了済みを覚えて適用自体を冪等にする
        // Initial application can arrive after a challenge completed during startup, so remember completions and make applying idempotent
        private readonly HashSet<Guid> _completedChallengeGuids = new();

        // 種別ごとの手配線は持たず、各managerの自己申告で引き当てる。種別追加はmanagerの登録1箇所で済む
        // No per-type wiring is kept; each manager names its own type, so adding one only touches its registration
        public TutorialManager(IReadOnlyList<ITutorialViewManager> tutorialViewManagers)
        {
            foreach (var tutorialViewManager in tutorialViewManagers) _tutorialViewManagers.Add(tutorialViewManager.TutorialType, tutorialViewManager);
        }
        
        public void ApplyTutorial(Guid challengeGuid)
        {
            if (_completedChallengeGuids.Contains(challengeGuid)) return;
            if (_tutorialViews.ContainsKey(challengeGuid)) return;

            var tutorialViews = new List<ITutorialView>();
            var challenge = MasterHolder.ChallengeMaster.GetChallenge(challengeGuid);

            // 平面表示sessionをworld viewと同じchallenge lifecycleで開始する
            // Start the flat presentation session in the same challenge lifecycle as world views
            if (WebUiScreenGate.IsWebUiMode)
                TutorialPresentationStateStore.Instance.BeginSession(challengeGuid);
            
            // チュートリアルを実際のManagerに適用する
            // Apply the tutorial to the actual Manager
            foreach (var tutorial in challenge.Tutorials)
            {
                var tutorialView = _tutorialViewManagers[tutorial.TutorialType].ApplyTutorial(tutorial);
                
                if (tutorialView != null) tutorialViews.Add(tutorialView);
            }
            
            _tutorialViews.Add(challengeGuid, tutorialViews);
        }
        
        public void CompleteChallenge(Guid challengeId)
        {
            _completedChallengeGuids.Add(challengeId);
            if (!_tutorialViews.TryGetValue(challengeId, out var tutorialViews)) return;

            // 平面表示を先にclearし、その後world viewを終了する
            // Clear flat presentations before completing the remaining world views
            if (WebUiScreenGate.IsWebUiMode)
            {
                TutorialPresentationStateStore.Instance.EndSession(challengeId);
            }
            
            foreach (var tutorialView in tutorialViews)
            {
                tutorialView.CompleteTutorial();
            }
            _tutorialViews.Remove(challengeId);
        }
    }
}
