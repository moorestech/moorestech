using System.Collections.Generic;
using UniRx;
using UnityEngine;

namespace Client.PlaytestReceiver.Recovery
{
    public sealed class PlaytestExportMissing
    {
        public readonly string Item;
        public readonly string Reason;
        public PlaytestExportMissing(string item, string reason) { Item = item; Reason = reason; }
    }

    public abstract class PlaytestExportStatus { }
    public sealed class PlaytestExportIdle : PlaytestExportStatus { public static readonly PlaytestExportIdle Instance = new(); }
    public sealed class PlaytestExportRunning : PlaytestExportStatus { public static readonly PlaytestExportRunning Instance = new(); }
    public sealed class PlaytestExportDone : PlaytestExportStatus
    {
        public readonly string ZipPath;
        public readonly int BoxCount;
        public readonly IReadOnlyList<PlaytestExportMissing> Missing;
        public readonly bool Revealed;
        public PlaytestExportDone(string zipPath, int boxCount, IReadOnlyList<PlaytestExportMissing> missing, bool revealed)
        { ZipPath = zipPath; BoxCount = boxCount; Missing = missing; Revealed = revealed; }
    }
    public sealed class PlaytestExportFailed : PlaytestExportStatus
    {
        public readonly string Reason;
        public PlaytestExportFailed(string reason) { Reason = reason; }
    }

    // 書き出しの進行をWeb UIへ渡す板。再生の跨ぎで初期値へ戻す
    // Board carrying export progress to the Web UI; resets between play sessions
    public static class PlaytestExportStatusBoard
    {
        private static readonly ReactiveProperty<PlaytestExportStatus> _current = new(PlaytestExportIdle.Instance);
        public static IReadOnlyReactiveProperty<PlaytestExportStatus> Current => _current;
        internal static void Set(PlaytestExportStatus status) { _current.Value = status; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        internal static void ResetOnPlayMode() { Set(PlaytestExportIdle.Instance); }
    }
}
