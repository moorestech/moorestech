using System;
using System.IO;
using Game.Paths;
using UnityEngine;

namespace Client.Game.InGame.BugReport.LastSession
{
    // 正常終了の意図が表明されたことだけを記録するマーカー。クラッシュは終了パイプラインに入れないので書かれない
    // A marker recording only that a graceful exit was intended; a crash never enters the shutdown pipeline, so it stays absent
    public static class CleanExitMarker
    {
        public const string FileName = "CLEAN_EXIT";

        public static string FilePath => Path.Combine(GameSystemPaths.BugReportLastSessionDirectory, FileName);

        // 起動時に1回だけ呼ぶ。読んだ時点で消して、次の終了で書き直させる
        // Call once at boot; the marker is removed on read so the next exit rewrites it
        public static bool ConsumePreviousExitCleanFlag()
        {
            var path = FilePath;
            var wasClean = File.Exists(path);
            if (wasClean) File.Delete(path);
            else Debug.LogWarning("前回の正常終了マーカーがありません。前回は異常終了として扱います");
            return wasClean;
        }

        public static void MarkCleanExit()
        {
            var path = FilePath;
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, DateTime.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'"));
        }
    }
}
