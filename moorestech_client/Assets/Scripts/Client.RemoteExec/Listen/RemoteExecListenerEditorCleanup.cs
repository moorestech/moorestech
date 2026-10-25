#if UNITY_EDITOR
using UnityEditor;

namespace Client.RemoteExec.Listen
{
    // Editor専用: Play終了・ドメインリロード・Editor終了のいずれでも待受を閉じる（WebUiHostEditorCleanupと同じ契機）
    // Editor-only: close the listener on play-mode exit, domain reload or editor quit (the same triggers as WebUiHostEditorCleanup)
    internal static class RemoteExecListenerEditorCleanup
    {
        [InitializeOnLoadMethod]
        private static void RegisterCleanupHooks()
        {
            AssemblyReloadEvents.beforeAssemblyReload += RemoteExecHttpListener.Stop;
            EditorApplication.quitting += RemoteExecHttpListener.Stop;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        // Reload Domain=offではbeforeAssemblyReloadが来ないため、Play終了の直前にも閉じる
        // With Reload Domain off beforeAssemblyReload never fires, so also close right before leaving play mode
        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingPlayMode) RemoteExecHttpListener.Stop();
        }
    }
}
#endif
