using System;
using System.Globalization;
using System.IO;

namespace Client.Game.InGame.BugReport.Recording.ProcessScope
{
    // 起動1回ぶんの識別。pidだけで割るとEditorの再生し直し（同じpid）が前セッションの録画・印・進行記録を持ち越すため、起動ごとに一意の段を足す
    // Identifies one boot; splitting by pid alone lets an Editor replay (same pid) inherit the previous session's recording, marks and progress, so a per-boot unique level is added
    // 綴りは <root>/pid_<PID>/session_<utcTicks>/。書き手は常に新しい段へ書き始め、回収はpid配下の全セッションを畳む（F05）
    // The layout is <root>/pid_<PID>/session_<utcTicks>/; writers always start in a fresh level, and collection folds every session under a pid (F05)
    public static class ProcessSessionScope
    {
        public const string SessionDirectoryPrefix = "session_";

        private static string _currentSessionName;
        private static long _lastSessionTicks;

        // 起動シーケンスの先頭で1回呼ぶ。以降この起動の書き手（録画・印・進行記録）は全員この名前の下へ書く
        // Called once at the head of the boot sequence; every writer of this boot (recording, marks, progress) writes under this name afterwards
        public static void BeginNewSession()
        {
            // 実世界の日時を識別子に使う用途でゲームロジックの経過時間ではない。同じtickに並んだら1つ進めて一意を保つ
            // The real-world clock is used as an identity, not as game-logic elapsed time; a tie is bumped by one to stay unique
            var ticks = DateTime.UtcNow.Ticks;
            if (ticks <= _lastSessionTicks) ticks = _lastSessionTicks + 1;
            _lastSessionTicks = ticks;
            _currentSessionName = SessionDirectoryPrefix + ticks.ToString(CultureInfo.InvariantCulture);
        }

        // 起動シーケンスを通らない経路（EditModeテスト）でも書き手同士が同じ名前を共有できるよう、未開始なら1度だけ始める
        // Starts once when nothing began yet, so writers on paths that skip the boot sequence (EditMode tests) still share one name
        public static string CurrentSessionName
        {
            get
            {
                if (_currentSessionName == null) BeginNewSession();
                return _currentSessionName;
            }
        }

        public static string SessionDirectoryFor(string root, int processId, string sessionName)
        {
            return Path.Combine(RecordingProcessDirectories.DirectoryFor(root, processId), sessionName);
        }

        public static string CurrentSessionDirectory(string root)
        {
            return SessionDirectoryFor(root, RecordingProcessDirectories.CurrentProcessId(), CurrentSessionName);
        }

        public static bool IsSessionDirectoryName(string name)
        {
            if (name == null || !name.StartsWith(SessionDirectoryPrefix, StringComparison.Ordinal)) return false;
            return long.TryParse(name.Substring(SessionDirectoryPrefix.Length), NumberStyles.None, CultureInfo.InvariantCulture, out _);
        }

        // 新しいセッションほど後ろに来る比較。utcTicksの桁数が揃わない将来でも数値で比べる
        // Orders newer sessions later, comparing numerically so a future change in tick digit count stays correct
        public static int CompareSessionNames(string left, string right)
        {
            var leftTicks = long.Parse(left.Substring(SessionDirectoryPrefix.Length), CultureInfo.InvariantCulture);
            var rightTicks = long.Parse(right.Substring(SessionDirectoryPrefix.Length), CultureInfo.InvariantCulture);
            return leftTicks.CompareTo(rightTicks);
        }
    }
}
