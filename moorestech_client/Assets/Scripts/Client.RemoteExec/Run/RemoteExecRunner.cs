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
        // client実行とserver実行で別の直列化にする。片方の長い実行がもう片方を待たせない
        // Client and server runs serialize separately, so a long run on one side never blocks the other
        private static readonly SemaphoreSlim ClientExecution = new(1, 1);
        private static readonly SemaphoreSlim ServerExecution = new(1, 1);

        // 台帳の対はHTTP入口と直接呼び出しの両方で同じRunnerが所有する
        // The runner owns the ledger pair for both HTTP and direct calls
        internal static async UniTask<RemoteExecResult> RunAsync(string body, RemoteExecTarget target, CancellationToken cancellationToken)
        {
            var execution = target == RemoteExecTarget.Server ? ServerExecution : ClientExecution;
            var acquired = false;
            var sequence = default(RemoteExecLedger.RemoteExecLedgerEntry);
            RemoteExecResult result;
            // 外部から送られ動的に実行するコードの第5境界で失敗を台帳へ閉じ込める
            // Isolate failure at the fifth boundary for dynamically executed submitted code
            try
            {
                // 実行先ごとにawait完了まで占有し、ログ捕捉の重複を防ぐ
                // Hold the target through completion to prevent overlapping log capture
                await execution.WaitAsync(cancellationToken);
                acquired = true;
                // 台帳のstart行は直列化を抜けて実際に開始する実行だけに対応させる
                // The ledger's start line corresponds only to a run that actually begins after serialization
                sequence = RemoteExecLedger.AppendStart(target, body);
                result = await RunCoreAsync(body, target, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                Debug.LogWarning("[RemoteExec] 切断済み要求の実行を取り消しました");
                result = RemoteExecResult.Cancelled("要求が取り消されました", null);
            }
            catch (Exception error)
            {
                Debug.LogError($"[RemoteExec] 実行要求に失敗しました: {error}");
                result = RemoteExecResult.RuntimeException(error.ToString(), null);
            }
            finally
            {
                if (acquired) execution.Release();
            }
            RemoteExecLedger.AppendResult(sequence, result.Outcome);
            return result;
        }

        private static async UniTask<RemoteExecResult> RunCoreAsync(string body, RemoteExecTarget target, CancellationToken cancellationToken)
        {
            // 参照解決とUnityログ購読はメインスレッドから始める
            // Start reference resolution and Unity log subscription on the main thread
            await UniTask.SwitchToMainThread(cancellationToken);

            cancellationToken.ThrowIfCancellationRequested();

            // 未知の実行先の拒否はRemoteExecEndpointの文字列検証1箇所に一本化する（ここへ来る時点でClient/Serverのいずれかである）
            // Rejecting an unknown target is centralized in RemoteExecEndpoint's string validation; by the time execution reaches here it is always Client or Server
            // 事前判定は無駄なコンパイル（Roslynは数百DLLを読む）を避けるためのもの。以後の受付失敗も同じ拒否文へ集約する
            // The pre-check avoids a wasted compile (Roslyn reads hundreds of DLLs); every later admission failure funnels into the same rejection text
            var queue = ServerThreadActionQueueAccess.Current;
            if (target == RemoteExecTarget.Server && (queue == null || !queue.HasDrainedThisLifetime))
                return ServerStopped(null);

            // Unity側の参照集合を使うコンパイルも同じスレッドで完了させる
            // Complete compilation with Unity's reference set on that same thread
            RemoteExecCompileOutcome outcome;
            // Roslynの動的コンパイルとAssembly.Loadは送られたコードの第5境界
            // Roslyn compilation and Assembly.Load are the fifth boundary for submitted external code
            try { outcome = RemoteExecCompiler.Compile(body); }
            catch (Exception error)
            {
                Debug.LogError($"[RemoteExec] コンパイル段に失敗しました: {error}");
                return RemoteExecResult.CompileFailed(new[] { error.GetBaseException().ToString() }, null);
            }
            if (!outcome.Succeeded) return RemoteExecResult.CompileFailed(outcome.Errors, null);

            var entry = outcome.Assembly.GetType(RemoteExecSourceWrapper.EntryTypeName)
                .GetMethod(RemoteExecSourceWrapper.EntryMethodName, BindingFlags.Public | BindingFlags.Static);

            // 受理直後のDrainで同期ログが漏れないよう、キュー投入より先に購読する
            // Subscribe before admission so an immediate drain cannot lose synchronous logs
            using var capture = new RemoteExecLogCapture();

            cancellationToken.ThrowIfCancellationRequested();
            // コンパイルは数百DLLを読むため秒単位かかる。その間のサーバー世代交代で旧キューへ積まないよう取り直す
            // Compilation takes seconds because it reads hundreds of DLLs, so the queue is re-fetched to avoid enqueueing onto a superseded server
            if (target == RemoteExecTarget.Server) queue = ServerThreadActionQueueAccess.Current;
            if (target == RemoteExecTarget.Server && (queue == null || !queue.HasDrainedThisLifetime)) return ServerStopped(capture);
            using var serverInvocation = target == RemoteExecTarget.Server
                ? new RemoteExecServerInvocation(entry, cancellationToken, queue) : null;
            if (target == RemoteExecTarget.Server && !queue.TryEnqueue(serverInvocation))
                return ServerStopped(capture);

            // 動的に実行する送信コードの第5境界で例外を応答へ隔離する
            // Isolate exceptions at the fifth boundary for dynamically executed submitted code
            try
            {
                object value;
                if (target == RemoteExecTarget.Client)
                {
                    value = await InvokeAsync(entry, cancellationToken);
                }
                else
                {
                    value = await serverInvocation.Completion;
                }

                // 結果の文字列化は送信コードの継続スレッド（server実行ならtickスレッド）で行わない
                // Never stringify the result on the submitted code's continuation thread, which is the tick thread for server runs
                await UniTask.SwitchToMainThread();
                return RemoteExecResult.Succeeded(value?.ToString(), capture.TakeLines());
            }
            catch (OperationCanceledException) when (serverInvocation != null && !serverInvocation.HasStarted && cancellationToken.IsCancellationRequested)
            {
                await UniTask.SwitchToMainThread();
                Debug.LogWarning("[RemoteExec] 実行前の要求を取り消しました");
                return RemoteExecResult.Cancelled("要求が取り消されました", capture.TakeLines());
            }
            catch (RemoteExecServerStoppedBeforeStartException)
            {
                await UniTask.SwitchToMainThread();
                return ServerStopped(capture);
            }
            catch (RemoteExecServerAbandonedException error)
            {
                await UniTask.SwitchToMainThread();
                Debug.LogError($"[RemoteExec] {error.Message}");
                return RemoteExecResult.ServerUnavailable(error.Message, capture.TakeLines());
            }
            catch (Exception error)
            {
                await UniTask.SwitchToMainThread();
                var exception = error.GetBaseException().ToString();
                Debug.LogWarning($"[RemoteExec] 送信コードの実行に失敗しました: {exception}");
                return RemoteExecResult.RuntimeException(exception, capture.TakeLines());
            }

            #region Internal

            // 事前判定・投入拒否・開始前停止の3経路を1つの拒否文へ集約し、ログと応答の形を1本に揃える
            // The pre-check, the refused admission and the stop-before-start funnel into one rejection text, keeping the log and response shape in one place
            RemoteExecResult ServerStopped(RemoteExecLogCapture logCapture)
            {
                const string reason = "内蔵サーバーが起動していないため、サーバー側では実行できません";
                Debug.LogWarning(reason);
                return RemoteExecResult.ServerUnavailable(reason, logCapture?.TakeLines());
            }

            async UniTask<object> InvokeAsync(MethodInfo method, CancellationToken token)
            {
                return await (UniTask<object>)method.Invoke(null, new object[] { token });
            }

            #endregion
        }
    }
}
