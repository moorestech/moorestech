using System;
using System.Collections.Generic;

namespace Server.Boot.Loop
{
    // 他スレッドからの処理をサーバーのtick末尾へ渡す
    // Hand work from other threads to the server tick end
    public static class ServerThreadActionQueue
    {
        private sealed class PendingAction
        {
            public readonly Action Run;
            public readonly Action OnStop;

            public PendingAction(Action run, Action onStop)
            {
                Run = run;
                OnStop = onStop;
            }
        }

        private static readonly object Gate = new();
        private static readonly Queue<PendingAction> Pending = new();
        private static bool _stopped = true;
        private static bool _hasDrainedThisLifetime;

        // 現サーバーのtick末尾が実際に動いた場合だけ受け付ける
        // Accept work only after this server has actually run a tick end
        public static bool HasDrainedThisLifetime
        {
            get { lock (Gate) return _hasDrainedThisLifetime; }
        }

        public static bool TryEnqueue(Action run, Action onStop)
        {
            lock (Gate)
            {
                if (_stopped || !_hasDrainedThisLifetime) return false;
                Pending.Enqueue(new PendingAction(run, onStop));
                return true;
            }
        }

        public static void Drain()
        {
            int count;
            lock (Gate)
            {
                if (_stopped) return;
                _hasDrainedThisLifetime = true;
                count = Pending.Count;
            }

            // 排出中の追加は次tickへ送る
            // Leave work added during draining for the next tick
            for (var index = 0; index < count; index++)
            {
                PendingAction item;
                lock (Gate)
                {
                    if (!_hasDrainedThisLifetime || Pending.Count == 0) return;
                    item = Pending.Dequeue();
                }
                item.Run();
            }
        }

        public static void ResetForNewServer()
        {
            Stop();
        }

        public static void BeginServerThread()
        {
            lock (Gate)
            {
                _stopped = false;
                _hasDrainedThisLifetime = false;
            }
        }

        public static void Stop()
        {
            var abandoned = new List<PendingAction>();
            lock (Gate)
            {
                _stopped = true;
                _hasDrainedThisLifetime = false;
                while (Pending.Count > 0) abandoned.Add(Pending.Dequeue());
            }

            // 待機者には寿命終了を伝えて完了させる
            // Complete waiters with a server-stopped outcome
            foreach (var item in abandoned) item.OnStop();
        }
    }
}
