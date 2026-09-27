using System;
using System.Reflection;
using System.Threading;
using Client.RemoteExec.Compile;
using Cysharp.Threading.Tasks;
using Server.Boot.Loop;
using UnityEngine;

namespace Client.RemoteExec.Run
{
    // コンパイルした本体を指定スレッドで開始し結果を組み立てる
    // Start compiled code on the chosen thread and build its outcome
    public static class RemoteExecRunner
    {
        private static readonly SemaphoreSlim ClientExecution = new(1, 1);

        public static async UniTask<RemoteExecResult> RunAsync(string body, RemoteExecTarget target)
        {
            if (target == RemoteExecTarget.Client)
            {
                // await中も占有し、クライアント実行の到着順を保つ
                // Hold admission across awaits to preserve client request order
                await ClientExecution.WaitAsync();
                try { return await RunCoreAsync(body, target); }
                finally { ClientExecution.Release(); }
            }

            return await RunCoreAsync(body, target);
        }

        private static async UniTask<RemoteExecResult> RunCoreAsync(string body, RemoteExecTarget target)
        {
            var result = new RemoteExecResult();
            // 参照解決とUnityログ購読はメインスレッドから始める
            // Start reference resolution and Unity log subscription on the main thread
            await UniTask.SwitchToMainThread();

            // 未知の実行先の拒否はRemoteExecEndpointの文字列検証1箇所に一本化する（ここへ来る時点でClient/Serverのいずれかである）
            // Rejecting an unknown target is centralized in RemoteExecEndpoint's string validation; by the time execution reaches here it is always Client or Server
            // 事前判定は無駄なコンパイル（Roslynは数百DLLを読む）を避けるためのもの。以後の受付失敗もRejectServerStoppedへ集約する
            // The pre-check avoids a wasted compile (Roslyn reads hundreds of DLLs); every later admission failure also funnels through RejectServerStopped
            if (target == RemoteExecTarget.Server && !ServerThreadActionQueue.HasDrainedThisLifetime)
            {
                RejectServerStopped();
                return result;
            }

            // Unity側の参照集合を使うコンパイルも同じスレッドで完了させる
            // Complete compilation with Unity's reference set on that same thread
            var outcome = RemoteExecCompiler.Compile(body);
            if (!outcome.Succeeded)
            {
                result.CompileErrors.AddRange(outcome.Errors);
                return result;
            }

            var entry = outcome.Assembly.GetType(RemoteExecSourceWrapper.EntryTypeName)
                .GetMethod(RemoteExecSourceWrapper.EntryMethodName, BindingFlags.Public | BindingFlags.Static);

            // ログキャプチャはtickキュー受理より前に作る。受理直後に別スレッドがDrainすると、
            // 購読前に流れた送信コードの同期ログが応答から漏れるため
            // The log capture must exist before tick-queue admission; if another thread drains
            // right after acceptance, the submitted code's synchronous logs would leak past an unsubscribed capture
            using var capture = new RemoteExecLogCapture();

            UniTaskCompletionSource<object> serverCompletion = null;
            if (target == RemoteExecTarget.Server)
            {
                serverCompletion = new UniTaskCompletionSource<object>();
                var accepted = ServerThreadActionQueue.TryEnqueue(
                    () => StartOnServerThread(entry, serverCompletion),
                    () => serverCompletion.TrySetException(new InvalidOperationException("内蔵サーバーが終了したため、サーバー側実行を取り消しました")));
                if (!accepted)
                {
                    RejectServerStopped();
                    return result;
                }
            }

            // 送信されたコードという外部入力の例外を応答へ隔離する
            // Isolate exceptions from submitted external code into the response
            try
            {
                object value;
                if (target == RemoteExecTarget.Client)
                {
                    // clientだけメインスレッドへ切り替える。serverはtickキューへ直行するので不要
                    // Only client switches to the main thread; server goes straight to the tick queue and needs none
                    await UniTask.SwitchToMainThread();
                    value = await InvokeAsync(entry);
                }
                else
                {
                    value = await serverCompletion.Task;
                }
                result.Ok = true;
                result.Result = value?.ToString();
            }
            catch (Exception error)
            {
                result.Exception = error.GetBaseException().ToString();
            }

            await UniTask.SwitchToMainThread();
            result.Logs.AddRange(capture.TakeLines());
            return result;

            #region Internal

            void RejectServerStopped()
            {
                // 事前判定とtickキュー拒否の両方をここへ集約し、文言・ログ・応答の形を1本に揃える
                // Both the pre-check and the tick-queue rejection funnel through here, keeping the wording, log and response shape in one place
                result.Exception = "内蔵サーバーが起動していないため、サーバー側では実行できません";
                Debug.LogWarning(result.Exception);
            }

            #endregion
        }

        private static async UniTask<object> InvokeAsync(MethodInfo entry)
        {
            return await (UniTask<object>)entry.Invoke(null, null);
        }

        private static void StartOnServerThread(MethodInfo entry, UniTaskCompletionSource<object> completion)
        {
            // 同期部分だけ更新スレッドで動く。UniTaskのYield/Delay後はPlayerLoopのメインスレッドで続く
            // Only the synchronous part runs on the update thread; UniTask Yield/Delay resumes on the main-thread PlayerLoop
            try
            {
                var invocation = (UniTask<object>)entry.Invoke(null, null);
                CompleteAsync(invocation, completion).Forget();
            }
            catch (Exception error)
            {
                completion.TrySetException(error);
            }
        }

        private static async UniTaskVoid CompleteAsync(UniTask<object> invocation, UniTaskCompletionSource<object> completion)
        {
            // 非同期側の例外もtickループへ漏らさず待機者に渡す
            // Route async exceptions to the waiter, not the tick loop
            try { completion.TrySetResult(await invocation); }
            catch (Exception error) { completion.TrySetException(error); }
        }
    }
}
