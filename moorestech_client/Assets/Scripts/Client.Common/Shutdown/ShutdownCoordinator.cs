using System;
using System.Collections.Generic;
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
                if (_shutdownTask != null) return _shutdownTask.AsUniTask();
                _shutdownTask = RunPipelineAsync().AsTask();
                return _shutdownTask.AsUniTask();
            }
        }

        // 登録された全ステップをフェーズ昇順→登録順で直列実行する
        // Run all registered steps sequentially, ordered by phase then registration order
        private static async UniTask RunPipelineAsync()
        {
            List<(ShutdownPhase phase, string name, Func<UniTask> step)> snapshot;
            lock (_lock) { snapshot = new List<(ShutdownPhase, string, Func<UniTask>)>(_steps); }
            snapshot.Sort((a, b) => a.phase.CompareTo(b.phase));

            foreach (var (phase, name, step) in snapshot)
            {
                Debug.Log($"[ShutdownCoordinator] [{phase}] {name} start");
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
