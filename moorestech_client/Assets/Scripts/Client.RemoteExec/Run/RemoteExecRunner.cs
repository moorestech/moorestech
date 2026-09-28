using System;
using System.Reflection;
using System.Threading;
using Client.RemoteExec.Access;
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
        private static readonly SemaphoreSlim Execution = new(1, 1);

        public static async UniTask<RemoteExecResult> RunAsync(string body, RemoteExecTarget target)
        {
            return await RunAsync(body, target, CancellationToken.None, null);
        }

        // 台帳の対はHTTP入口と直接呼び出しの両方で同じRunnerが所有する
        // The runner owns the ledger pair for both HTTP and direct calls
        internal static async UniTask<RemoteExecResult> RunAsync(string body, RemoteExecTarget target, CancellationToken cancellationToken, IRemoteExecHttpRunner testRunner)
        {
            var sequence = RemoteExecLedger.AppendStart(target, body);
            RemoteExecResult result;
            // 外部から送られ動的に実行するコードの境界で失敗を台帳へ閉じ込める
            // Isolate failure at the execution boundary for dynamically compiled external code
            try
            {
                result = testRunner == null ? await ExecuteAsync(body, target, cancellationToken) : await testRunner.RunAsync(body, target, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                Debug.LogWarning("[RemoteExec] 切断済み要求の実行を取り消しました");
                result = new RemoteExecResult { Outcome = RemoteExecOutcome.Rejected, Exception = "要求が取り消されました" };
            }
            catch (Exception error)
            {
                Debug.LogError($"[RemoteExec] 実行要求に失敗しました: {error}");
                result = RemoteExecResult.FromUnhandledException(error);
            }
            RemoteExecLedger.AppendResult(sequence, result.Outcome);
            return result;
        }

        internal static async UniTask<RemoteExecResult> ExecuteAsync(string body, RemoteExecTarget target, CancellationToken cancellationToken)
        {
            // 全実行先をawait完了まで占有し、ログ捕捉の重複を防ぐ
            // Hold all targets through completion to prevent overlapping log capture
            await Execution.WaitAsync(cancellationToken);
            try { return await RunCoreAsync(body, target, cancellationToken); }
            finally { Execution.Release(); }
        }

        private static async UniTask<RemoteExecResult> RunCoreAsync(string body, RemoteExecTarget target, CancellationToken cancellationToken)
        {
            var result = new RemoteExecResult();
            // 参照解決とUnityログ購読はメインスレッドから始める
            // Start reference resolution and Unity log subscription on the main thread
            await UniTask.SwitchToMainThread(cancellationToken);

            cancellationToken.ThrowIfCancellationRequested();

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
            RemoteExecCompileOutcome outcome;
            // Roslynの動的コンパイルとAssembly.Loadは外部送信コードを扱う境界
            // Roslyn compilation and Assembly.Load form the boundary for submitted external code
            try { outcome = RemoteExecCompiler.Compile(body); }
            catch (Exception error)
            {
                Debug.LogError($"[RemoteExec] コンパイル段に失敗しました: {error}");
                result.Outcome = RemoteExecOutcome.CompileFailed;
                result.CompileErrors.Add(error.GetBaseException().ToString());
                return result;
            }
            if (!outcome.Succeeded)
            {
                result.Outcome = RemoteExecOutcome.CompileFailed;
                result.CompileErrors.AddRange(outcome.Errors);
                return result;
            }

            var entry = outcome.Assembly.GetType(RemoteExecSourceWrapper.EntryTypeName)
                .GetMethod(RemoteExecSourceWrapper.EntryMethodName, BindingFlags.Public | BindingFlags.Static);

            // 受理直後のDrainで同期ログが漏れないよう、キュー投入より先に購読する
            // Subscribe before admission so an immediate drain cannot lose synchronous logs
            using var capture = new RemoteExecLogCapture();

            cancellationToken.ThrowIfCancellationRequested();
            using var serverInvocation = target == RemoteExecTarget.Server
                ? new RemoteExecServerInvocation(entry, cancellationToken) : null;
            if (target == RemoteExecTarget.Server)
            {
                var accepted = ServerThreadActionQueue.TryEnqueue(
                    serverInvocation.StartOnServerThread, serverInvocation.StopBeforeStart);
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
                    value = await InvokeAsync(entry);
                }
                else
                {
                    value = await serverInvocation.Completion;
                }
                result.Result = value?.ToString();
                result.Outcome = RemoteExecOutcome.Succeeded;
            }
            catch (OperationCanceledException) when (serverInvocation != null && !serverInvocation.HasStarted && cancellationToken.IsCancellationRequested)
            {
                Debug.LogWarning("[RemoteExec] 実行前の要求を取り消しました");
                result.Exception = "要求が取り消されました";
                result.Outcome = RemoteExecOutcome.Rejected;
            }
            catch (Exception error)
            {
                result.Exception = error.GetBaseException().ToString();
                result.Outcome = RemoteExecOutcome.RuntimeException;
                Debug.LogWarning($"[RemoteExec] 送信コードの実行に失敗しました: {result.Exception}");
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
                result.Outcome = RemoteExecOutcome.Rejected;
                Debug.LogWarning(result.Exception);
            }

            async UniTask<object> InvokeAsync(MethodInfo method)
            {
                return await (UniTask<object>)method.Invoke(null, null);
            }

            #endregion
        }

    }
}
