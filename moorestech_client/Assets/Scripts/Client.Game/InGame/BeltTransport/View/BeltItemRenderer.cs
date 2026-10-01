using Client.Game.Common;
using Core.BeltTransport;
using Cysharp.Threading.Tasks;
using UniRx;
namespace Client.Game.InGame.BeltTransport
{
    public sealed class BeltItemRenderer : IInitialEventApplyWaitTarget
    {
        private readonly BeltNetworkEventHandler _network;
        private readonly BeltItemViewStore _views;
        private UniTask _initial;
        public BeltItemRenderer(BeltNetworkEventHandler network, IBeltItemViewFactory factory)
        { _network = network; _views = new BeltItemViewStore(factory); }
        public void Initialize()
        {
            // ClientContext完成後に描画を開始し、snapshotより後の通知も同じ所有者へ渡す。
            // Start rendering after ClientContext is ready and route later states to the same owner.
            _network.Replica.OnStateChanged.Subscribe(Apply);
            _initial = _views.ApplyAsync(_network.Replica.Snapshot).Preserve();

            #region Internal
            void Apply(BeltNetworkSnapshot snapshot) => _views.ApplyAsync(snapshot).Forget();
            #endregion
        }
        public async UniTask WaitForInitialApplyAsync()
        {
            // バッファ再生で追加された初回表示も完了まで待つ。
            // Wait for initial views added while buffered events were replayed.
            await _initial;
            await _views.WaitForPendingAsync();
        }
    }
}
