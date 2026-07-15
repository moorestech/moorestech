using UnityEngine;
using UnityEngine.InputSystem;

namespace Client.Playtest.Overlay
{
    /// <summary>
    ///     オーバーレイへの静的窓口。View未生成でも状態更新は安全に受け付ける
    ///     Static entry point to the overlay; state updates are safe even before the view exists
    /// </summary>
    public static class PlaytestOverlay
    {
        public static readonly PlaytestOverlayState State = new();
        private static PlaytestOverlayView _view;

        public static void EnsureCreatedAndReset()
        {
            // ラン開始ごとに状態を初期化し、Viewは1つだけ生成して使い回す
            // Reset state per run; create the view once and reuse it
            State.Reset();
            if (!Application.isPlaying || _view != null) return;
            var overlayObject = new GameObject("PlaytestOverlay");
            Object.DontDestroyOnLoad(overlayObject);
            _view = overlayObject.AddComponent<PlaytestOverlayView>();
        }

        public static void PushStep(string text) => State.PushLog(PlaytestOverlayLogKind.Step, text);
        public static void PushNote(string text) => State.PushLog(PlaytestOverlayLogKind.Note, text);
        public static PlaytestOverlayLogEntry PushWait(string text) => State.PushLog(PlaytestOverlayLogKind.Wait, text);
        public static void PushAssert(string label, bool passed) => State.PushLog(passed ? PlaytestOverlayLogKind.AssertPass : PlaytestOverlayLogKind.AssertFail, $"{(passed ? "✔" : "✘")} {label}");

        // Digit1→1のように表示ラベルを短縮する
        // Shorten display labels, e.g. Digit1 -> 1
        public static void NotifyKey(Key key, bool down) => State.NotifyKey(key.ToString().Replace("Digit", ""), down);
        public static void NotifyMouseButton(int button, bool down) => State.NotifyMouseButton(button, down);
        public static void NotifyAllKeysReleased() => State.HeldKeys.Clear();
    }
}
