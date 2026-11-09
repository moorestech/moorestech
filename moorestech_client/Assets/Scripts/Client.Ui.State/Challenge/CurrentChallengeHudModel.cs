using System.Collections.Generic;
using Client.Ui.State.SkitPresence;
using Client.Ui.State.Text;
using Mooresmaster.Localization.Generated;
using UniRx;
using VContainer.Unity;

namespace Client.Ui.State.Challenge
{
    /// <summary>
    /// 全カテゴリの進行中チャレンジを受信順に平らにし、完了と blocking スキットの変化で作り直す（Web の challenge.current topic）
    /// Flattens every category's current challenges in received order and rebuilds on completions and blocking-skit changes (Web challenge.current topic)
    /// </summary>
    public sealed class CurrentChallengeHudModel : IInitializable
    {
        private readonly ChallengeProgressState _progressState;
        private readonly BlockingSkitPresence _blockingSkitPresence;
        private readonly ReactiveProperty<CurrentChallengeHudViewModel> _current;

        public IReadOnlyReactiveProperty<CurrentChallengeHudViewModel> Current => _current;

        public CurrentChallengeHudModel(ChallengeProgressState progressState, BlockingSkitPresence blockingSkitPresence)
        {
            _progressState = progressState;
            _blockingSkitPresence = blockingSkitPresence;
            _current = new ReactiveProperty<CurrentChallengeHudViewModel>(Build());
        }

        public void Initialize()
        {
            // 完了の知らせは Categories を置き換えた後に来る。ゲーム寿命なので購読は解かない
            // The completion notice arrives after Categories is replaced; it lives for the whole game, so subscriptions stay
            _progressState.OnChallengeCompleted.Subscribe(_ => Rebuild());
            _blockingSkitPresence.IsBlocking.Skip(1).Subscribe(_ => Rebuild());
            Rebuild();

            #region Internal

            void Rebuild()
            {
                _current.SetValueAndForceNotify(Build());
            }

            #endregion
        }

        private CurrentChallengeHudViewModel Build()
        {
            // カテゴリ順×受信順（ChallengeDtoBuilder.BuildCurrent と同じ並び）。題はマスタの内容キー
            // Category order times received order (same as ChallengeDtoBuilder.BuildCurrent); titles are master content keys
            var objectives = new List<UiText>();
            foreach (var category in _progressState.Categories)
            foreach (var challenge in category.CurrentChallenges)
                objectives.Add(UiText.FromContent(ContentLocalizationKeys.ChallengeTitle(challenge.ChallengeGuid)));
            return CurrentChallengeHudViewModel.Create(objectives, _blockingSkitPresence.IsBlocking.Value);
        }
    }
}
