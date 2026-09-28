using System.Collections.Generic;
using Core.Update;

namespace Server.Boot.Loop
{
    // 他スレッドからの処理をサーバーのtick末尾へ渡す。1インスタンス＝1サーバー寿命
    // Hand work from other threads to the server tick end; one instance lives for one server
    public sealed class ServerThreadActionQueue
    {
        private readonly object _gate = new();
        private readonly Queue<IServerThreadAction> _pending = new();
        // 受理済みで未解放の処理。Stopはこれを列挙して停止を届ける
        // Admitted work not yet released; Stop enumerates it to deliver the stop notification
        private readonly Dictionary<IServerThreadAction, ulong> _liveStartTicks = new();
        private readonly HashSet<IServerThreadAction> _stallReported = new();
        private bool _stopped;
        private bool _hasDrainedThisLifetime;

        // 現サーバーのtick末尾が実際に動いた場合だけ受け付ける
        // Accept work only after this server has actually run a tick end
        public bool HasDrainedThisLifetime
        {
            get { lock (_gate) return _hasDrainedThisLifetime; }
        }

        // 返らない処理を検知した事実。打ち切りはせず、無音停止だけを防ぐ
        // Whether a non-returning action was detected; nothing is aborted, only the silence is prevented
        public bool IsStalled { get; private set; }

        public bool TryEnqueue(IServerThreadAction action)
        {
            lock (_gate)
            {
                if (_stopped || !_hasDrainedThisLifetime) return false;
                _pending.Enqueue(action);
                _liveStartTicks[action] = ulong.MaxValue;
                return true;
            }
        }

        // 実行し終えた処理を受理集合から外す。外さないとサーバー寿命の間だけ積み上がる
        // Drop a finished action from the admitted set; otherwise it piles up for the server's lifetime
        public void Release(IServerThreadAction action)
        {
            lock (_gate)
            {
                _liveStartTicks.Remove(action);
                _stallReported.Remove(action);
            }
        }

        public void Drain()
        {
            var stalled = new List<IServerThreadAction>();
            int count;
            lock (_gate)
            {
                if (_stopped) return;
                _hasDrainedThisLifetime = true;
                count = _pending.Count;
                CollectStalledLocked(stalled);
            }

            // 返らない処理は打ち切らず記録だけ残す（セーブ確定点が無音で止まるのを防ぐ）
            // A non-returning action is recorded rather than aborted, so the save-stable point never stops silently
            foreach (var item in stalled)
                UnityEngine.Debug.LogError($"[ServerThreadActionQueue] tick末尾の処理が次tickまでに終わりませんでした。セーブ確定点とスナップショットが止まります action:{item.GetType().Name}");

            // 排出中の追加は次tickへ送る
            // Leave work added during draining for the next tick
            for (var index = 0; index < count; index++)
            {
                IServerThreadAction item;
                lock (_gate)
                {
                    if (_stopped || _pending.Count == 0) return;
                    item = _pending.Dequeue();
                    // 停止と実行開始を同じ排他状態で確定する。Stopはこの後も同じ項目へ停止を届けられる
                    // Start and stop are decided under one exclusion; Stop can still reach this item afterwards
                    _liveStartTicks[item] = GameUpdater.CurrentTick;
                }
                item.Run();
            }
        }

        public void Stop()
        {
            var abandoned = new List<IServerThreadAction>();
            lock (_gate)
            {
                if (_stopped) return;
                _stopped = true;
                _hasDrainedThisLifetime = false;
                _pending.Clear();
                abandoned.AddRange(_liveStartTicks.Keys);
                _liveStartTicks.Clear();
                _stallReported.Clear();
            }

            // 未開始・開始済みのどちらにも寿命終了を伝える。どう畳むかは処理自身が決める
            // Tell both unstarted and started work that the lifetime ended; each action decides how to fold
            foreach (var item in abandoned) item.OnServerStopped();
        }

        private void CollectStalledLocked(List<IServerThreadAction> stalled)
        {
            var currentTick = GameUpdater.CurrentTick;
            foreach (var entry in _liveStartTicks)
            {
                if (entry.Value == ulong.MaxValue || currentTick <= entry.Value) continue;
                if (!_stallReported.Add(entry.Key)) continue;
                IsStalled = true;
                stalled.Add(entry.Key);
            }
        }
    }
}
