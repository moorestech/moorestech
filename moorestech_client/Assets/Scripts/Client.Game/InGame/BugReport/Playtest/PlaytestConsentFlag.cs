using System;
using System.Globalization;
using System.IO;
using Game.Paths;

namespace Client.Game.InGame.BugReport.Playtest
{
    // 同意表示の既読フラグ。同意そのものはキー配布時の案内で取っており、これは表示を1回に絞るためのローカル印
    // The consent screen's read flag; consent itself is taken at key handout, and this local mark only limits the display to once
    public static class PlaytestConsentFlag
    {
        public const string FileName = "consent-acknowledged-v1";

        public static string FilePath => Path.Combine(GameSystemPaths.BugReportDirectory, FileName);

        public static bool IsAcknowledged()
        {
            return File.Exists(FilePath);
        }

        public static void Acknowledge()
        {
            Directory.CreateDirectory(GameSystemPaths.BugReportDirectory);
            File.WriteAllText(FilePath, DateTime.UtcNow.ToString(BugReportBundleLayout.Utc8601Format, CultureInfo.InvariantCulture));
        }
    }
}
