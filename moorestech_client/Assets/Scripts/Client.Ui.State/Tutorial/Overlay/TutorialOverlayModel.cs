using Client.Game.InGame.Tutorial;
using UniRx;
using VContainer.Unity;

namespace Client.Ui.State.Tutorial.Overlay
{
    public sealed class TutorialOverlayModel : IInitializable
    {
        private readonly TutorialPresentationStateStore _store;
        private readonly TutorialOverlayProjection _projection = new();
        private readonly ReactiveProperty<TutorialOverlayViewModel> _current = new(TutorialOverlayViewModel.Empty);

        public IReadOnlyReactiveProperty<TutorialOverlayViewModel> Current => _current;

        public TutorialOverlayModel(TutorialPresentationStateStore store)
        {
            _store = store;
        }

        public void Initialize()
        {
            // 演出データが変わるたび枠と DragGuide を組み直す
            // Rebuild outlines and drag guides whenever the presentation data changes
            _store.ObserveChanged().Subscribe(_ => Rebuild());

            // ctor から購読までの間の変化を取り込む
            // Pick up any change made between construction and subscription
            Rebuild();
        }

        private void Rebuild()
        {
            _current.Value = _projection.Project(_store.GetCurrent());
        }
    }
}
