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
        private static long _generation;

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
            long generation;
            lock (Gate)
            {
                if (_stopped) return;
                _hasDrainedThisLifetime = true;
                generation = _generation;
                count = Pending.Count;
            }

            // 排出中の追加は次tickへ送る。世代が変わった分は新サーバーのDrainに任せる
            // Leave work added during draining for the next tick; a changed generation is left to the new server's own Drain
            for (var index = 0; index < count; index++)
            {
                PendingAction item;
                lock (Gate)
                {
                    if (_stopped || generation != _generation || Pending.Count == 0) return;
                    item = Pending.Dequeue();
                }
                item.Run();
            }
        }

        internal static long BeginServerThread()
        {
            lock (Gate)
            {
                _generation++;
                _stopped = false;
                _hasDrainedThisLifetime = false;
                return _generation;
            }
        }

        internal static void Stop()
        {
            StopGeneration(0, false);
        }

        // 古い更新スレッドの終了では、新しいサーバーの受付を閉じない
        // An old update thread cannot close the queue of a newer server
        internal static void Stop(long generation)
        {
            StopGeneration(generation, true);
        }

        private static void StopGeneration(long generation, bool checkGeneration)
        {
            var abandoned = new List<PendingAction>();
            lock (Gate)
            {
                if (checkGeneration && generation != _generation)
                {
                    UnityEngine.Debug.Log($"[ServerThreadActionQueue] 古い世代の停止を無視しました generation:{generation} current:{_generation}");
                    return;
                }
                _stopped = true;
                _hasDrainedThisLifetime = false;
                while (0 < Pending.Count) abandoned.Add(Pending.Dequeue());
            }

            // 待機者には寿命終了を伝えて完了させる
            // Complete waiters with a server-stopped outcome
            foreach (var item in abandoned) item.OnStop();
        }
    }
}
