using System;
using Cysharp.Threading.Tasks;
using Server.Boot.Loop;
using UnityEngine;

namespace Client.Playtest.Core
{
    /// <summary>
    ///     DSLのサーバー状態変更をサーバーtick末尾で実行し、完了をメインスレッドで待つ。tickと別スレッドで書くと列挙中のコレクションを壊すため
    ///     Runs a DSL server-state mutation at the server tick end and awaits it on the main thread; writing off the tick thread corrupts collections mid-enumeration
    /// </summary>
    public sealed class PlaytestServerThreadAction : IServerThreadAction
    {
        // tickは50ms刻みなので、これを超えて戻らないのはtickが止まっている
        // Ticks run every 50 ms, so not returning within this means the tick has stalled
        private const float CompletionTimeoutSeconds = 10f;

        private readonly string _label;
        private readonly Action _serverAction;
        private readonly UniTaskCompletionSource _completion = new();
        private ServerThreadActionQueue _queue;

        private PlaytestServerThreadAction(string label, Action serverAction)
        {
            _label = label;
            _serverAction = serverAction;
        }

        public static async UniTask RunAsync(string label, Action serverAction)
        {
            // 現サーバーのtick末尾へ渡し、完了後はメインスレッドへ戻ってから次の操作へ進む
            // Hand the work to the current server's tick end and return to the main thread before the next operation
            var action = new PlaytestServerThreadAction(label, serverAction);
            action._queue = ServerThreadActionQueueAccess.Current;
            if (action._queue == null || !action._queue.TryEnqueue(action))
            {
                Debug.LogError($"[Playtest] サーバーがtick末尾の処理を受理できません: {label}");
                throw new InvalidOperationException($"The running server did not accept the playtest server action: {label}");
            }
            await action._completion.Task.Timeout(TimeSpan.FromSeconds(CompletionTimeoutSeconds));
            await UniTask.SwitchToMainThread();
        }

        public void Run()
        {
            // シナリオは外部から送られ動的にコンパイルされたコードで、その本体をここで実行する。例外をtickへ漏らすと待ち手が10秒止まるため、実行境界で捕まえて待ち手へ渡す
            // Scenarios are externally sent, dynamically compiled code whose body runs here; leaking an exception into the tick would stall the awaiter for 10 s, so catch it at this execution boundary and hand it over
            try
            {
                _serverAction();
            }
            catch (Exception exception)
            {
                Debug.LogError($"[Playtest] tick末尾の処理が例外で失敗しました: {_label}\n{exception}");
                _queue.Release(this);
                _completion.TrySetException(exception);
                return;
            }
            _queue.Release(this);
            _completion.TrySetResult();
        }

        public void OnServerStopped()
        {
            Debug.LogError($"[Playtest] 処理完了前にサーバーが停止しました: {_label}");
            _completion.TrySetException(new InvalidOperationException($"Server stopped before the playtest server action completed: {_label}"));
        }
    }
}
