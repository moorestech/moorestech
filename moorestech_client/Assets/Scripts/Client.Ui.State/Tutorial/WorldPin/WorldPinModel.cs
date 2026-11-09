using Client.Game.InGame.Tutorial;
using Client.Ui.State.SkitPresence;
using UniRx;
using VContainer.Unity;

namespace Client.Ui.State.Tutorial.WorldPin
{
    public sealed class WorldPinModel : IInitializable
    {
        private readonly BlockingSkitPresence _blockingSkit;
        private readonly WorldPinStateStore _store;
        private readonly WorldPinViewModelProjection _projection = new();
        private readonly ReactiveProperty<WorldPinViewModel> _current = new(WorldPinViewModel.Empty);

        public IReadOnlyReactiveProperty<WorldPinViewModel> Current => _current;

        public WorldPinModel(BlockingSkitPresence blockingSkit, WorldPinStateStore store)
        {
            _blockingSkit = blockingSkit;
            _store = store;
        }

        public void Initialize()
        {
            // ピンの射影結果かスキットが変わるたび組み直す
            // Rebuild whenever the pin data or the skit changes
            _store.ObserveChanged().Subscribe(_ => Rebuild());
            _blockingSkit.IsBlocking.Skip(1).Subscribe(_ => Rebuild());

            // ctor から購読までの間の変化を取り込む
            // Pick up any change made between construction and subscription
            Rebuild();
        }

        private void Rebuild()
        {
            _current.Value = _projection.Project(_store.GetCurrent(), _blockingSkit.IsBlocking.Value);
        }
    }
}
