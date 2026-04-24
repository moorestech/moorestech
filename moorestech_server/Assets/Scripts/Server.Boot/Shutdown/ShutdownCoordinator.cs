using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Server.Boot.Shutdown
{
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
        internal static void ResetForTests()
        {
            lock (_lock) { _steps.Clear(); _shutdownTask = null; }
        }
#endif
    }
}
