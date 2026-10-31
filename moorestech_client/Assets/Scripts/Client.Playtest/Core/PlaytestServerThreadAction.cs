using System;
using System.Threading;
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

        private const int StateQueued = 0;
        private const int StateStarted = 1;
        private const int StateCancelled = 2;

        private readonly string _label;
        private readonly Action _serverAction;
        private readonly UniTaskCompletionSource _completion = new();
        private ServerThreadActionQueue _queue;

        // 開始と取り消しをtickスレッドとメインスレッドで奪い合うので、一度だけ遷移させる
        // The tick and main threads race to start or cancel, so the state transitions exactly once
        private int _state = StateQueued;

        // 待ち手へ渡す失敗。nullなら成功として完了させる
        // The failure handed to the awaiter; null completes it as a success
        private Exception _failure;

        private PlaytestServerThreadAction(string label, Action serverAction)
        {
            _label = label;
            _serverAction = serverAction;
        }

        public static async UniTask RunAsync(CancellationToken cancellationToken, string label, Action serverAction)
        {
            // 現サーバーのtick末尾へ渡す。受理されなければ即座に失敗を返す
            // Hand the work to the current server's tick end; fail immediately when it is not admitted
            if (cancellationToken.IsCancellationRequested)
            {
                Debug.LogError($"[Playtest] 終了済みシナリオのサーバー操作を拒否しました: {label}");
                cancellationToken.ThrowIfCancellationRequested();
            }
            var action = new PlaytestServerThreadAction(label, serverAction);
            action._queue = ServerThreadActionQueueAccess.Current;
            if (action._queue == null || !action._queue.TryEnqueue(action))
            {
                Debug.LogError($"[Playtest] サーバーがtick末尾の処理を受理できません: {label}");
                throw new InvalidOperationException($"The running server did not accept the playtest server action: {label}");
            }

            // 時間切れなら未開始の処理を取り消す。timeScaleに左右されない実時間で測る
            // On timeout cancel the not-yet-started work; measure in real time so timeScale cannot stretch it
            using var cancellationRegistration = cancellationToken.Register(action.CancelOnScenarioEnd);
            using var timeoutSource = new CancellationTokenSource();
            timeoutSource.Token.Register(action.CancelOnTimeout);
            await action._completion.Task.Timeout(TimeSpan.FromSeconds(CompletionTimeoutSeconds), DelayType.Realtime, PlayerLoopTiming.Update, timeoutSource);
        }

        public void Run()
        {
            // 時間切れで取り消された処理は、失敗として報告済みなので後から実行しない
            // Work cancelled by timeout was already reported as failed, so never run it late
            if (Interlocked.CompareExchange(ref _state, StateStarted, StateQueued) != StateQueued) return;

            // シナリオは外部から送られ動的にコンパイルされたコードで、その本体をここで実行する。例外をtickへ漏らすと待ち手が10秒止まるため、実行境界で捕まえて待ち手へ渡す
            // Scenarios are externally sent, dynamically compiled code whose body runs here; leaking an exception into the tick would stall the awaiter for 10 s, so catch it at this execution boundary and hand it over
            try
            {
                _serverAction();
            }
            catch (Exception exception)
            {
                Debug.LogError($"[Playtest] tick末尾の処理が例外で失敗しました: {_label}\n{exception}");
                _failure = exception;
            }
            _queue.Release(this);
            CompleteOnMainThread();
        }

        public void OnServerStopped()
        {
            // 未開始なら取り消す。Drainが受理確認を通った直後に停止が来ても、失敗と報告した処理を後から走らせない
            // Cancel it if not started, so work reported as failed never runs late even when the stop lands right after Drain admitted it
            if (Interlocked.CompareExchange(ref _state, StateCancelled, StateQueued) == StateQueued)
                Debug.LogError($"[Playtest] 未開始のままサーバーが停止したため取り消しました: {_label}");
            else
                Debug.LogError($"[Playtest] 処理完了前にサーバーが停止しました: {_label}");
            _failure = new InvalidOperationException($"Server stopped before the playtest server action completed: {_label}");
            CompleteOnMainThread();
        }

        private void CancelOnScenarioEnd()
        {
            // 終了済みシナリオの未開始操作はキューから除く
            // Release queued work when its owning scenario ends
            if (Interlocked.CompareExchange(ref _state, StateCancelled, StateQueued) == StateQueued)
            {
                _queue.Release(this);
                Debug.LogError($"[Playtest] シナリオ終了で未開始の操作を取り消しました: {_label}");
            }
            else
                Debug.LogError($"[Playtest] シナリオ終了時に操作が開始済みです: {_label}");
            _failure = new OperationCanceledException($"Scenario ended before server action completed: {_label}");
            CompleteOnMainThread();
        }

        private void CancelOnTimeout()
        {
            // 未開始なら取り消してキューから外す。開始済みならtickの中で詰まっているので、その旨だけ残す
            // If not started, cancel it and drop it from the queue; if started, it is stuck inside the tick, so just record that
            if (Interlocked.CompareExchange(ref _state, StateCancelled, StateQueued) == StateQueued)
            {
                _queue.Release(this);
                Debug.LogError($"[Playtest] tick末尾の処理が{CompletionTimeoutSeconds}秒以内に始まらないため取り消しました: {_label}");
                return;
            }
            Debug.LogError($"[Playtest] tick末尾の処理が{CompletionTimeoutSeconds}秒以内に終わりません（tickの中で止まっています）: {_label}");
        }

        private void CompleteOnMainThread()
        {
            // 完了源は続きを完了させたスレッドで同期実行する。成功・失敗とも続きをメインスレッドで走らせるため、通知自体をメインスレッドへ渡す
            // The completion source runs continuations synchronously on the completing thread; post the notification itself so success and failure both continue on the main thread
            if (PlayerLoopHelper.IsMainThread)
            {
                PublishCompletion();
                return;
            }
            UniTask.Post(PublishCompletion, PlayerLoopTiming.Update);
        }

        private void PublishCompletion()
        {
            if (_failure != null)
            {
                _completion.TrySetException(_failure);
                return;
            }
            _completion.TrySetResult();
        }
    }
}
