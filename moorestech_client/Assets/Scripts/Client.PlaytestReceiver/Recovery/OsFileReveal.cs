using System;
using System.ComponentModel;
using System.Diagnostics;

namespace Client.PlaytestReceiver.Recovery
{
    public interface IPlaytestFileReveal
    {
        // ファイルを選択状態で開く。開けなければfalse
        // Opens the file selected in the file manager; false when it could not
        bool Reveal(string filePath);
    }

    public sealed class OsFileReveal : IPlaytestFileReveal
    {
        public bool Reveal(string filePath)
        {
            // 外部プロセス起動境界。開けなくてもzipは出来ているので、パスを画面に出して続ける
            // External process boundary: the zip already exists, so a failure only leaves the path on screen
            try
            {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
                Process.Start("explorer.exe", $"/select,\"{filePath}\"");
#elif UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
                Process.Start("open", $"-R \"{filePath}\"");
#else
                UnityEngine.Debug.LogWarning($"[PlaytestReceiver] reveal is not supported on this platform: {filePath}");
                return false;
#endif
                return true;
            }
            catch (Exception error) when (error is Win32Exception || error is InvalidOperationException)
            {
                UnityEngine.Debug.LogWarning($"[PlaytestReceiver] cannot reveal {filePath}: {error.Message}");
                return false;
            }
        }
    }
}
