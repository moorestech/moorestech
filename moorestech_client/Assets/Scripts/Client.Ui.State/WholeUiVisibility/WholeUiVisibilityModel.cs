using UniRx;
using VContainer.Unity;

namespace Client.Ui.State.WholeUiVisibility
{
    /// <summary>
    /// UITK 全体を出すか。Web App.tsx の「uiVisible かつ カットシーンでない」（A-80）
    /// Whether the whole UITK UI shows; the Web App.tsx "uiVisible and not a cutscene" (A-80)
    /// </summary>
    public sealed class WholeUiVisibilityModel : IInitializable
    {
        private readonly IWholeUiVisibilitySource _source;
        private readonly ReactiveProperty<bool> _isShown = new(true);

        public IReadOnlyReactiveProperty<bool> IsShown => _isShown;

        public WholeUiVisibilityModel(IWholeUiVisibilitySource source)
        {
            _source = source;
        }

        public void Initialize()
        {
            // ゲームと同じ寿命なので購読は張りっぱなし
            // Same lifetime as the game, so the subscription is held for good
            _source.ObserveUiRootVisible()
                .CombineLatest(_source.ObserveCutScenePlaying(), (visible, cutScene) => visible && !cutScene)
                .Subscribe(shown => _isShown.Value = shown);
        }
    }
}
