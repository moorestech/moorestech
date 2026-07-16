#if UNITY_EDITOR
using System;
using Client.WebUiHost.Vite;
using UnityEditor;

namespace Client.WebUiHost.Boot
{
    /// <summary>
    /// Editor 専用: Play mode 終了・ドメインリロード・エディタ終了のいずれでも WebUiHost を確実に停止する
    /// Editor-only: reliably stops WebUiHost on play-mode exit, domain reload, or editor quit
    /// </summary>
    public static class WebUiHostEditorCleanup
    {
        //   - ExitingPlayMode: Reload Domain = off の場合に beforeAssemblyReload が来ない穴を埋める
        //   - beforeAssemblyReload: Reload Domain = on の通常経路
        //   - quitting: Play mode に入らずにエディタを閉じた場合
        //   - ExitingPlayMode: covers the gap where beforeAssemblyReload never fires with Reload Domain off
        //   - beforeAssemblyReload: the normal path with Reload Domain on
        //   - quitting: the editor is closed without entering play mode
        [InitializeOnLoadMethod]
        private static void RegisterDomainReloadHook()
        {
            AssemblyReloadEvents.beforeAssemblyReload += CleanupAllSync;
            EditorApplication.quitting += CleanupAllSync;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            // Play mode 終了直前にクリーンアップ。Domain Reload が走る前にポート解放を完了させる
            // Clean up just before exiting play mode so ports are released before Domain Reload
            if (state == PlayModeStateChange.ExitingPlayMode)
            {
                CleanupAllSync();
            }
        }

        // Editor hook は同期完了を要求するので、共有の停止シーケンスを最大 4 秒待つ
        // Editor hooks need synchronous completion; wait up to 4s on the shared stop sequence
        private static void CleanupAllSync()
        {
            WebUiHost.StopAndWaitSync(TimeSpan.FromSeconds(4));

            // セーフティネット: Vite が残っていたら SessionState の記録から特定して kill
            // Safety net: if any Vite is still alive, resolve it from the SessionState record and kill
            ViteProcessKiller.KillAnyLingering();
        }
    }
}
#endif
