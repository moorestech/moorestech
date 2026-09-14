using System.Collections.Generic;

namespace Client.Game.InGame.BugReport.Recording.ProcessScope
{
    // 引き継ぎ対象の1件。pidは退避先の階層名と正常終了マーカーの割り当てにそのまま使う
    // One directory to take over; the pid is reused verbatim for the salvage layout and for the clean-exit marker
    public sealed class RecordingProcessDirectory
    {
        public int ProcessId;
        public string Path;
    }

    // 引き継ぎの選別結果。触らなかったものも理由が読めるよう持ち帰る
    // The selection's result; what was left untouched comes back too so the reason stays readable
    public sealed class RecordingProcessTakeover
    {
        public List<RecordingProcessDirectory> Directories = new();
        public List<int> SkippedLiveProcessIds = new();
        public List<string> UnknownDirectories = new();
    }
}
