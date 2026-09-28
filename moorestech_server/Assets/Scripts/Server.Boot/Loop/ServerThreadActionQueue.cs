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
        private readonly HashSet<IServerThreadAction> _admitted = new();
        private bool _stopped;
        private bool _hasDrainedThisLifetime;

        // 現サーバーのtick末尾が実際に動いた場合だけ受け付ける
        // Accept work only after this server has actually run a tick end
        public bool HasDrainedThisLifetime
        {
            get { lock (_gate) return _hasDrainedThisLifetime; }
        }

        public bool TryEnqueue(IServerThreadAction action)
        {
            lock (_gate)
            {
                if (_stopped || !_hasDrainedThisLifetime) return false;
                _pending.Enqueue(action);
                _admitted.Add(action);
                return true;
            }
        }

        // 実行し終えた処理を受理集合から外す。外さないとサーバー寿命の間だけ積み上がる
        // Drop a finished action from the admitted set; otherwise it piles up for the server's lifetime
        public void Release(IServerThreadAction action)
        {
            lock (_gate) _admitted.Remove(action);
        }

        public void Drain()
        {
            int count;
            lock (_gate)
            {
                if (_stopped) return;
                _hasDrainedThisLifetime = true;
                count = _pending.Count;
            }

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
                    if (!_admitted.Contains(item)) continue;
                }

                // 開始と復帰を対で残す。tickを止めた処理はtick自身が進まないので時間では測れず、復帰ログの無い開始ログだけが証跡になる
                // Log entry and return as a pair; an action that froze the tick cannot be timed by ticks it stopped, so an entry line without its return line is the only evidence
                UnityEngine.Debug.Log($"[ServerThreadActionQueue] tick末尾の処理を開始します action:{item.GetType().Name} tick:{GameUpdater.CurrentTick}");
                item.Run();
                UnityEngine.Debug.Log($"[ServerThreadActionQueue] tick末尾の処理から復帰しました action:{item.GetType().Name} tick:{GameUpdater.CurrentTick}");
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
                abandoned.AddRange(_admitted);
                _admitted.Clear();
            }

            // 未開始・開始済みのどちらにも寿命終了を伝える。どう畳むかは処理自身が決める
            // Tell both unstarted and started work that the lifetime ended; each action decides how to fold
            foreach (var item in abandoned) item.OnServerStopped();
        }
    }
}
