using System.IO;
using UnityEngine;

namespace Client.PlaytestReceiver.Recovery
{
    public interface IPlaytestExportDestination
    {
        // 書き出し先のディレクトリ。取れなければnull
        // Directory receiving the export, or null when unavailable
        string ResolveDirectory();
    }

    // テスターがそのままドラッグで提出できるようデスクトップへ置く
    // Put the zip on the desktop so testers can drag it straight into their upload
    public sealed class DesktopExportDestination : IPlaytestExportDestination
    {
        public string ResolveDirectory()
        {
            // Client.* の同名名前空間と衝突するため完全修飾する
            // Fully qualified because Client.* has a same-named namespace
            var desktop = System.Environment.GetFolderPath(System.Environment.SpecialFolder.Desktop);
            if (!string.IsNullOrEmpty(desktop) && Directory.Exists(desktop)) return desktop;
            Debug.LogWarning($"[PlaytestReceiver] desktop folder unavailable: '{desktop}'");
            return null;
        }
    }
}
