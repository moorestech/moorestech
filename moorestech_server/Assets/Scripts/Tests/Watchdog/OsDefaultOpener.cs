using System;
using System.Diagnostics;
using System.IO;
using Debug = UnityEngine.Debug;

namespace Tests.Watchdog
{
    public static class OsDefaultOpener
    {
        public static void OpenWithDefaultApp(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                Debug.LogError("path is null/empty");
                return;
            }
            
            path = Path.GetFullPath(path);
            
            if (!File.Exists(path) && !Directory.Exists(path))
            {
                Debug.LogError($"Not found: {path}");
                return;
            }
            
            try
            {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            // Windows: 既定アプリで開く
            Process.Start(new ProcessStartInfo(path)
            {
                UseShellExecute = true
            });
                
#elif UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
                // macOS: open コマンド
                Process.Start("open", $"\"{path}\"");
                
#elif UNITY_STANDALONE_LINUX || UNITY_EDITOR_LINUX
            // Linux: xdg-open コマンド
            Process.Start("xdg-open", $"\"{path}\"");
                
#else
            // それ以外（環境により動かないことがあります）
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
#endif
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }
    }
}