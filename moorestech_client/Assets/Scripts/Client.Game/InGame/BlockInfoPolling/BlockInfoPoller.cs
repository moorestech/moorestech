using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Block.Interface;
using UniRx;
using UnityEngine;

namespace Client.Game.InGame.BlockInfoPolling
{
    /// <summary>
    ///     表示中の1ブロックについてサーバーの集約情報を一定間隔で問い合わせ、未取得／取得済み／消失を保つ
    ///     Polls the server aggregate for one displayed block at a fixed interval, keeping it not fetched, fetched or vanished
    /// </summary>
    public class BlockInfoPoller<TResponse, TSnapshot> where TResponse : class
    {
        public BlockInfoFetchState State { get; private set; } = BlockInfoFetchState.NotFetched;

        // 取得済みになった・消失したときに発火する（通信失敗では発火しない）
        // Fires when it becomes fetched or vanished; never on a transport failure
        public IObservable<Unit> OnUpdated => _onUpdated;

        private readonly Subject<Unit> _onUpdated = new();
        private readonly IBlockInfoPollSource<TResponse, TSnapshot> _source;
        private readonly string _requestName;
        private readonly TimeSpan _interval;
        private CancellationTokenSource _cts;
        private TSnapshot _snapshot;

        public BlockInfoPoller(IBlockInfoPollSource<TResponse, TSnapshot> source, string requestName, TimeSpan interval)
        {
            _source = source;
            _requestName = requestName;
            _interval = interval;
        }

        // 対象ブロックを替えて問い合わせを始め直す。前の結果は捨てて未取得に戻す
        // Restart polling for a new block, dropping the previous result back to not fetched
        public void Start(BlockInstanceId blockInstanceId)
        {
            Stop();
            _cts = new CancellationTokenSource();
            PollLoop(blockInstanceId, _cts.Token).Forget();
        }

        public void Stop()
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
            State = BlockInfoFetchState.NotFetched;
            _snapshot = default;
        }

        // 取得済みのときだけ値を返す
        // Returns the value only while fetched
        public bool TryGetSnapshot(out TSnapshot snapshot)
        {
            snapshot = _snapshot;
            return State == BlockInfoFetchState.Fetched;
        }

        private async UniTaskVoid PollLoop(BlockInstanceId blockInstanceId, CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                var response = await _source.Request(blockInstanceId, ct);
                if (ct.IsCancellationRequested) return;

                // 消失したら問い合わせを止める。次の Start までは消失のままなので、警告は1回だけ出る
                // Stop polling once vanished; it stays vanished until the next Start, so the warning is logged once
                if (!Apply(response, blockInstanceId)) return;
                await UniTask.Delay(_interval, cancellationToken: ct);
            }
        }

        // 問い合わせを続けるなら true、ブロック消失なら false
        // True to keep polling, false once the block has vanished
        private bool Apply(TResponse response, BlockInstanceId blockInstanceId)
        {
            // 通信失敗は状態と前回値を保つ（未取得なら未取得のまま）。開発者には警告で知らせる
            // A transport failure keeps the state and last value (not fetched stays so); warn the developer
            if (response == null)
            {
                Debug.LogWarning($"[BlockInfoPoller] {_requestName} failed for block {blockInstanceId}; keeping the {State} state");
                return true;
            }

            // ブロック消失（または読めない応答）は値を捨てて消失にする
            // A vanished block (or an unreadable response) drops the value and turns vanished
            if (!_source.TryGetSnapshot(response, out var snapshot))
            {
                Debug.LogWarning($"[BlockInfoPoller] {_requestName} found no block {blockInstanceId} (or an unreadable response); clearing the snapshot and stopping");
                _snapshot = default;
                State = BlockInfoFetchState.Vanished;
                _onUpdated.OnNext(Unit.Default);
                return false;
            }

            _snapshot = snapshot;
            State = BlockInfoFetchState.Fetched;
            _onUpdated.OnNext(Unit.Default);
            return true;
        }
    }
}
