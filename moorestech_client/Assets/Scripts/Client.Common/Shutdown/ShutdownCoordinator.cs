using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Client.Common.Shutdown
{
    // 終了パイプラインのオーケストレーション。Unity 非依存の async API のみを公開
    // Orchestrates the shutdown pipeline with a Unity-independent async API
    public static class ShutdownCoordinator
    {
        private static readonly object _lock = new();
        private static readonly List<(ShutdownPhase phase, string name, Func<UniTask> step)> _steps = new();
        private static Task _shutdownTask;

        // Reload Domain = disabled でも静的状態を初期化し、次プレイセッションで Register が ignored にならないようにする
        // Reset static state even when Reload Domain is disabled, so the next play session can Register participants
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlaySessionStart()
        {
            lock (_lock) { _steps.Clear(); _shutdownTask = null; }
        }

        public static void Register(ShutdownPhase phase, string name, Func<UniTask> step)
        {
            lock (_lock)
            {
                if (_shutdownTask != null)
                {
                    Debug.LogWarning($"[ShutdownCoordinator] Register ignored after shutdown started: {name}");
                    return;
                }
                _steps.Add((phase, name, step));
            }
        }

        public static UniTask ShutdownAsync()
        {
            lock (_lock)
            {
                // AsUniTask(false) で SynchronizationContext をキャプチャしない
                // Bridge が Task.Run でスレッドプール実行する経路では main thread の
                // SyncContext が利用不可で TaskScheduler.FromCurrentSynchronizationContext が失敗するため
                // Do not capture SynchronizationContext: Bridge runs this on a thread pool via Task.Run,
                // where the main-thread sync context cannot be resolved by TaskScheduler.FromCurrentSynchronizationContext
                if (_shutdownTask != null) return _shutdownTask.AsUniTask(false);
                _shutdownTask = RunPipelineAsync().AsTask();
                return _shutdownTask.AsUniTask(false);
            }
        }

        // 登録された全ステップをフェーズ昇順→登録順で直列実行する
        // Run all registered steps sequentially, ordered by phase then registration order
        private static async UniTask RunPipelineAsync()
        {
            List<(ShutdownPhase phase, string name, Func<UniTask> step)> snapshot;
            lock (_lock) { snapshot = new List<(ShutdownPhase, string, Func<UniTask>)>(_steps); }
            // OrderBy は stable なので同一フェーズ内の登録順が保たれる
            // OrderBy is stable, so registration order within the same phase is preserved
            var sorted = snapshot.OrderBy(s => s.phase).ToList();

            foreach (var (phase, name, step) in sorted)
            {
                Debug.Log($"[ShutdownCoordinator] [{phase}] {name} start");
                // try-catch はここだけの例外。1 ステップ失敗でもパイプラインを継続して残りの片付けを完遂させる設計契約
                // Intentional try-catch: shutdown must continue even if a step throws, so the remaining cleanup runs
                try
                {
                    await step();
                    Debug.Log($"[ShutdownCoordinator] [{phase}] {name} done");
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex);
                    Debug.LogError($"[ShutdownCoordinator] [{phase}] {name} failed, continuing");
                }
            }
        }

#if UNITY_INCLUDE_TESTS
        // テスト用。複数テストの間でグローバル状態をリセットする
        // Test-only hook to reset global state between tests
        internal static void ResetForTests()
        {
            lock (_lock) { _steps.Clear(); _shutdownTask = null; }
        }
#endif
    }
}
