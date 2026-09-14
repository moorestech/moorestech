using System.Collections.Generic;

namespace Client.Game.InGame.BugReport.LastSession
{
    // 共有置き場はOSが全アプリのクラッシュを溜める場所で、自プロセス名で絞らないと他アプリの記録まで運んでしまう
    // A shared root is where the OS piles every app's crash, so without a process-name filter other apps' records get shipped
    public sealed class CrashDumpRoot
    {
        public string Path;
        public bool SharedWithOtherApps;
    }

    // 選別の入力。ファイルの実在に依らず選別規則だけを試せるよう、置き場と名前の対だけを持つ
    // The selection's input; it holds only the root-and-name pair so the rule can be exercised without real files
    public sealed class CrashDumpCandidate
    {
        public CrashDumpRoot Root;
        public string FileName;
        public string FullPath;
    }

    // 選別の結果。落とした件数と落とした置き場を持ち帰り、除外を無音の縮退にしない
    // The selection's result; it carries what was dropped and from where so the exclusion is never a silent degradation
    public sealed class CrashDumpScanResult
    {
        public List<string> Files = new();
        public int ExcludedAsOtherApps;

        // 時刻境界で落とした件数。無音で捨てると「ダンプが無い」と「境界で捨てた」を開発者が区別できない
        // How many were dropped by the time boundary; dropping them silently would make "no dump" and "filtered by time" indistinguishable
        public int ExcludedAsTooOld;
        public List<string> ExcludedRoots = new();
    }
}
