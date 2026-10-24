using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Block.Interface;
using Server.Protocol.PacketResponse;
using UniRx;
using UnityEngine;

namespace Client.Game.InGame.BlockInfoPolling
{
    /// <summary>
    ///     表示中の1ブロックについてサーバーの集約情報を一定間隔で問い合わせ、取得状態と最後に読めた値を保つ
    ///     Polls the server aggregate for one displayed block at a fixed interval, keeping the fetch state and the last value read
    /// </summary>
    public class BlockInfoPoller<TResponse, TSnapshot> where TResponse : class
    {
        public BlockInfoFetchState State { get; private set; } = BlockInfoFetchState.NotFetched;

        // 応答ごと（通信失敗も含む）に状態が決まったときに発火する
        // Fires whenever a response (including a transport failure) settles the state
        public IObservable<Unit> OnBlockInfoSnapshotUpdated => _onBlockInfoSnapshotUpdated;

        private readonly Subject<Unit> _onBlockInfoSnapshotUpdated = new();
        private readonly IBlockInfoPollSource<TResponse, TSnapshot> _source;
        private readonly string _requestName;
        private readonly TimeSpan _interval;
        private CancellationTokenSource _cts;
        private TSnapshot _snapshot;
        private bool _hasSnapshot;

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

            #region Internal

            async UniTaskVoid PollLoop(BlockInstanceId blockInstanceId, CancellationToken ct)
            {
                while (!ct.IsCancellationRequested)
                {
                    var response = await _source.Request(blockInstanceId, ct);
                    if (ct.IsCancellationRequested) return;

                    // 消失したときだけ問い合わせを止める。次の Start までは消失のままなので、その警告は1回だけ出る
                    // Stop polling only once vanished; it stays vanished until the next Start, so that warning is logged once
                    if (!Apply(response, blockInstanceId)) return;
                    await UniTask.Delay(_interval, cancellationToken: ct);
                }

                #region Internal

                // 問い合わせを続けるなら true、ブロック消失なら false
                // True to keep polling, false once the block has vanished
                bool Apply(TResponse response, BlockInstanceId blockInstanceId)
                {
                    // 通信失敗は最後に読めた値を保ったまま取得失敗にし、次の間隔で再試行する
                    // A transport failure keeps the last value read, turns transport-failed and retries at the next interval
                    if (response == null)
                    {
                        Debug.LogWarning($"[BlockInfoPoller] {_requestName} failed for block {blockInstanceId}; marking it transport-failed and retrying");
                        SetState(BlockInfoFetchState.TransportFailed);
                        return true;
                    }

                    var result = _source.TryRead(response, out var snapshot);
                    switch (result)
                    {
                        case BlockInfoReadResult.Success:
                            _snapshot = snapshot;
                            _hasSnapshot = true;
                            SetState(BlockInfoFetchState.Fetched);
                            return true;
                        case BlockInfoReadResult.Unreadable:
                            Debug.LogWarning($"[BlockInfoPoller] {_requestName} got an unreadable response for block {blockInstanceId}; dropping the value and retrying");
                            DropSnapshotAndSetState(BlockInfoFetchState.Unreadable);
                            return true;
                        case BlockInfoReadResult.UnsupportedTarget:
                            Debug.LogWarning($"[BlockInfoPoller] {_requestName} does not support block {blockInstanceId}; dropping the value and retrying");
                            DropSnapshotAndSetState(BlockInfoFetchState.UnsupportedTarget);
                            return true;
                        case BlockInfoReadResult.BlockNotFound:
                            Debug.LogWarning($"[BlockInfoPoller] {_requestName} found no block {blockInstanceId}; clearing the snapshot and stopping");
                            DropSnapshotAndSetState(BlockInfoFetchState.Vanished);
                            return false;
                        default:
                            throw new ArgumentOutOfRangeException(nameof(result), result, "未対応の読み取り結果 / Unsupported read result");
                    }
                }

                void DropSnapshotAndSetState(BlockInfoFetchState state)
                {
                    _snapshot = default;
                    _hasSnapshot = false;
                    SetState(state);
                }

                void SetState(BlockInfoFetchState state)
                {
                    State = state;
                    _onBlockInfoSnapshotUpdated.OnNext(Unit.Default);
                }

                #endregion
            }

            #endregion
        }

        public void Stop()
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
            State = BlockInfoFetchState.NotFetched;
            _snapshot = default;
            _hasSnapshot = false;
        }

        // 最後に正常に読めた値を返す。通信失敗の間も保つので、最新かどうかは State か GetLatest で見分ける
        // Returns the last value read successfully; it survives transport failures, so tell freshness by State or GetLatest
        public bool TryGetSnapshot(out TSnapshot snapshot)
        {
            snapshot = _snapshot;
            return _hasSnapshot;
        }

        // 取得状態を返し、最新の正常値は Fetched のときだけ渡す（取得できていない間に古い値を最新と誤認させない）
        // Returns the fetch state and hands out the latest valid value only while Fetched, so a stale value is never mistaken for the latest
        public BlockInfoFetchState GetLatest(out TSnapshot latestSnapshot)
        {
            latestSnapshot = State == BlockInfoFetchState.Fetched ? _snapshot : default;
            return State;
        }
    }
}
