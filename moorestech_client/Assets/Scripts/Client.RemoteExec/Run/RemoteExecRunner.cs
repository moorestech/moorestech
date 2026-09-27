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
            await UniTask.SwitchToMainThread();

            if (target != RemoteExecTarget.Client && target != RemoteExecTarget.Server)
            {
                result.Exception = $"未知の実行先です: {target}";
                Debug.LogWarning(result.Exception);
                return result;
            }

            if (target == RemoteExecTarget.Server && !ServerThreadActionQueue.HasDrainedThisLifetime)
            {
                result.Exception = "内蔵サーバーが起動していないため、サーバー側では実行できません";
                Debug.LogWarning(result.Exception);
                return result;
            }

            var outcome = RemoteExecCompiler.Compile(body);
            if (!outcome.Succeeded)
            {
                result.CompileErrors.AddRange(outcome.Errors);
                return result;
            }

            var entry = outcome.Assembly.GetType(RemoteExecSourceWrapper.EntryTypeName)
                .GetMethod(RemoteExecSourceWrapper.EntryMethodName, BindingFlags.Public | BindingFlags.Static);
            using var capture = new RemoteExecLogCapture();

            // 送信されたコードという外部入力の例外を応答へ隔離する
            // Isolate exceptions from submitted external code into the response
            try
            {
                var value = target == RemoteExecTarget.Client
                    ? await InvokeAsync(entry)
                    : await RunOnServerThreadAsync(entry);
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
        }

        private static async UniTask<object> InvokeAsync(MethodInfo entry)
        {
            return await (UniTask<object>)entry.Invoke(null, null);
        }

        private static async UniTask<object> RunOnServerThreadAsync(MethodInfo entry)
        {
            var completion = new UniTaskCompletionSource<object>();
            var accepted = ServerThreadActionQueue.TryEnqueue(
                () => StartOnServerThread(entry, completion),
                () => completion.TrySetException(new InvalidOperationException("内蔵サーバーが終了したため、サーバー側実行を取り消しました")));
            if (!accepted)
            {
                Debug.LogWarning("内蔵サーバーが停止中のため、サーバー側実行を受け付けません");
                throw new InvalidOperationException("内蔵サーバーが起動していないため、サーバー側では実行できません");
            }
            return await completion.Task;
        }

        private static void StartOnServerThread(MethodInfo entry, UniTaskCompletionSource<object> completion)
        {
            // 同期部分は更新スレッドで動く。await後の継続先は送信コードが選ぶ
            // The synchronous part runs on the update thread; submitted awaits determine continuations
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
