using System;
using Client.Game.Common;
using Core.BeltTransport;
using Cysharp.Threading.Tasks;
using UniRx;
using UnityEngine;
namespace Client.Game.InGame.BeltTransport
{
    public sealed class BeltItemRenderer : IInitialEventApplyWaitTarget
    {
        private readonly BeltNetworkEventHandler _network;
        private readonly BeltItemViewStore _views;
        private UniTask _initial;
        private Exception _failure;
        public BeltItemRenderer(BeltNetworkEventHandler network, IBeltItemViewFactory factory)
        { _network = network; _views = new BeltItemViewStore(factory); }
        public void Initialize()
        {
            // ClientContext完成後に描画を開始し、snapshotより後の通知も同じ所有者へ渡す。
            // Start rendering after ClientContext is ready and route later states to the same owner.
            _network.Replica.OnStateChanged.Subscribe(Apply);
            _initial = _views.ApplyAsync(_network.Replica.Snapshot).Preserve();
        }
        public async UniTask WaitForInitialApplyAsync()
        {
            _network.ThrowIfFailed();
            // バッファ再生中に追加された生成も待ち、受信失敗時はロード待ちを打ち切る。
            // Include creations added during buffered replay and stop waiting when reception fails.
            await UniTask.WhenAny(WaitForViewsAsync(), _network.WaitForFailureAsync());
            _network.ThrowIfFailed();
            if (_failure != null) throw new InvalidOperationException("Belt item rendering failed.", _failure);
        }
        private async UniTask WaitForViewsAsync()
        {
            await _initial;
            await _views.WaitForPendingAsync();
        }
        private void Apply(BeltNetworkSnapshot snapshot) => ApplyAsync(snapshot).Forget();
        private async UniTask ApplyAsync(BeltNetworkSnapshot snapshot)
        {
            // Addressables非同期ロード境界の失敗を表示し、初期待機にも伝える。
            // Report failures at the asynchronous Addressables boundary and fail startup waiting.
            try { await _views.ApplyAsync(snapshot); }
            catch (Exception error) { _failure = error; Debug.LogError($"Belt item rendering failed: {error}"); }
        }
    }
}
