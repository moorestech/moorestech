using System.IO;
using UnityEngine;

namespace Client.Game.InGame.BugReport.LastSession
{
    // 前回セッションのUnityログ。Unityは起動時に前回のPlayer.logをPlayer-prev.logへ回すのでそれを拾う
    // The previous session's Unity log; Unity rotates the old Player.log to Player-prev.log at boot, so that is what we take
    public static class PlayerLogLocator
    {
        public const string PreviousLogFileName = "Player-prev.log";

        public static string PreviousSessionLogPath()
        {
            // Editorでは Editor-prev.log ではなく consoleLogPath の隣を見る。どちらの実行形態も同じ規則で解決する
            // In the Editor this looks next to consoleLogPath as well, resolving both run modes by the same rule
            var currentLogPath = Application.consoleLogPath;
            if (string.IsNullOrEmpty(currentLogPath)) return null;
            var directory = Path.GetDirectoryName(currentLogPath);
            if (directory == null) return null;
            var candidate = Path.Combine(directory, PreviousLogFileName);
            return File.Exists(candidate) ? candidate : null;
        }
    }
}
