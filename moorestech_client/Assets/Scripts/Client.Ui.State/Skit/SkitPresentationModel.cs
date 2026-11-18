using Client.Skit.UI;
using UniRx;
using VContainer.Unity;

namespace Client.Ui.State.Skit
{
    /// <summary>
    /// store の提示を購読して SkitViewModel にして配る。画面に何を出すかの正は store のモード（A-81）
    /// Subscribes to the store's presentation and hands it out as a SkitViewModel; the store mode is the source of truth for what shows (A-81)
    /// </summary>
    public sealed class SkitPresentationModel : IInitializable
    {
        private readonly SkitPresentationStateStore _store;
        private readonly ReactiveProperty<SkitViewModel> _current = new(SkitViewModel.Empty);

        public IReadOnlyReactiveProperty<SkitViewModel> Current => _current;

        public SkitPresentationModel(SkitPresentationStateStore store)
        {
            _store = store;
        }

        public void Initialize()
        {
            // store の通知は購読時に現在値を流さないので、今の提示を先に入れる。購読はゲームと同じ寿命で張りっぱなし
            // The store's stream does not replay on subscribe, so feed the current snapshot first; the subscription lives as long as the game
            _store.ObserveChanged().StartWith(_store.GetCurrent()).Subscribe(data => _current.Value = SkitViewModel.From(data));
        }
    }
}
