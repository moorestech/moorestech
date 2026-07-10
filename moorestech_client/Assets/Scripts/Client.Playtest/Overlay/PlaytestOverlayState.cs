using System.Collections.Generic;
using UnityEngine;

namespace Client.Playtest.Overlay
{
    // ログ種別（描画色の切り替えに使う）
    // Log entry kind (drives the draw color)
    public enum PlaytestOverlayLogKind { Step, Note, Wait, AssertPass, AssertFail }

    public class PlaytestOverlayLogEntry
    {
        public PlaytestOverlayLogKind Kind;
        public string Text;
        public float ElapsedSeconds;
    }

    public class PlaytestOverlayInputEntry
    {
        public string Label;
        public float PushedRealtime;
    }

    /// <summary>
    ///     オーバーレイ表示用の状態置き場。SemanticInputとDriverが書き込みViewが毎フレーム読む
    ///     State store for the overlay: written by SemanticInput/Driver, read by the view every frame
    /// </summary>
    public class PlaytestOverlayState
    {
        public const int MaxLogEntries = 9;
        public const float RecentInputLifetime = 2f;
        private const int MaxRecentInputs = 8;

        public readonly List<PlaytestOverlayLogEntry> LogEntries = new();
        public readonly List<string> HeldKeys = new();
        public readonly List<PlaytestOverlayInputEntry> RecentInputs = new();
        public float RunStartRealtime;
        public float LastClickRealtime = -10f;
        public bool LeftMouseHeld;
        public bool RightMouseHeld;

        public void Reset()
        {
            LogEntries.Clear();
            HeldKeys.Clear();
            RecentInputs.Clear();
            RunStartRealtime = Time.realtimeSinceStartup;
            LastClickRealtime = -10f;
            LeftMouseHeld = false;
            RightMouseHeld = false;
        }

        public PlaytestOverlayLogEntry PushLog(PlaytestOverlayLogKind kind, string text)
        {
            // スタック式: 末尾が最新、上限を超えたら最古を落とす
            // Stack style: newest at the tail; drop the oldest beyond the cap
            var entry = new PlaytestOverlayLogEntry { Kind = kind, Text = text, ElapsedSeconds = Time.realtimeSinceStartup - RunStartRealtime };
            LogEntries.Add(entry);
            if (MaxLogEntries < LogEntries.Count) LogEntries.RemoveAt(0);
            return entry;
        }

        public void NotifyKey(string label, bool down)
        {
            if (down)
            {
                if (!HeldKeys.Contains(label)) HeldKeys.Add(label);
                PushRecentInput(label);
            }
            else
            {
                HeldKeys.Remove(label);
            }
        }

        public void NotifyMouseButton(int button, bool down)
        {
            // 左ボタンの解放時刻はクリックリップルの描画に使う
            // The left-button release time drives the click ripple rendering
            if (button == 0)
            {
                LeftMouseHeld = down;
                if (!down) LastClickRealtime = Time.realtimeSinceStartup;
            }
            if (button == 1) RightMouseHeld = down;
            if (down) PushRecentInput(button == 0 ? "LMB" : button == 1 ? "RMB" : "MMB");
        }

        private void PushRecentInput(string label)
        {
            RecentInputs.Add(new PlaytestOverlayInputEntry { Label = label, PushedRealtime = Time.realtimeSinceStartup });
            if (MaxRecentInputs < RecentInputs.Count) RecentInputs.RemoveAt(0);
        }
    }
}
