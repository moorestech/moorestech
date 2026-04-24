using System;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Server.Boot.Shutdown
{
    internal static class ApplicationShutdownBridge
    {
        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void InstallRuntimeHooks()
        {
            Application.quitting -= TriggerBlocking;
            Application.quitting += TriggerBlocking;
        }

#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
        private static void InstallEditorHooks()
        {
            UnityEditor.EditorApplication.quitting -= TriggerBlocking;
            UnityEditor.EditorApplication.quitting += TriggerBlocking;
            UnityEditor.AssemblyReloadEvents.beforeAssemblyReload -= TriggerBlocking;
            UnityEditor.AssemblyReloadEvents.beforeAssemblyReload += TriggerBlocking;
            UnityEditor.EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            UnityEditor.EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private static void OnPlayModeStateChanged(UnityEditor.PlayModeStateChange state)
        {
            if (state == UnityEditor.PlayModeStateChange.ExitingPlayMode) TriggerBlocking();
        }
#endif

        // パイプラインを背景スレッドで走らせる。メインスレッドで待つと、
        // PlayerLoopTiming に戻る UniTask 継続がメインスレッドの解放を待って deadlock するため
        // Run the pipeline on a background thread; waiting on the main thread deadlocks
        // any UniTask continuation that resumes on PlayerLoopTiming
        private static void TriggerBlocking()
        {
            var task = Task.Run(() => ShutdownCoordinator.ShutdownAsync().AsTask());
            task.Wait(Timeout);
            if (task.IsFaulted) Debug.LogException(task.Exception?.GetBaseException());
            if (!task.IsCompleted) Debug.LogWarning("[ApplicationShutdownBridge] shutdown timed out");
        }
    }
}
