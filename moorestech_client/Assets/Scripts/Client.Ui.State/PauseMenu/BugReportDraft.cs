using Client.Game.InGame.BugReport.Playtest;

namespace Client.Ui.State.PauseMenu
{
    /// <summary>
    /// バグ報告の書きかけ（本文と種別）。ページを作り直しても残るよう View の外に置く（D-7）
    /// The bug-report draft (description and kind), kept outside the view so it survives page rebuilds (D-7)
    /// </summary>
    public sealed class BugReportDraft
    {
        public string Description { get; private set; } = "";
        public PlaytestReportKind Kind { get; private set; } = PlaytestReportKind.Bug;

        // Reset のたびに進む世代。送信の完了が、その後に書かれた別の書きかけを消さないための照合に使う
        // Advances on every Reset; lets a send completion avoid wiping a different draft written after the send began
        public int Generation { get; private set; }

        public void SetDescription(string description)
        {
            Description = description;
        }

        public void SetKind(PlaytestReportKind kind)
        {
            Kind = kind;
        }

        // 種別も既定へ戻す。残すと次の1件が前回の種別のまま箱詰めされる（ADR 0058）
        // Reset the kind too: leaving it boxes the next report under the previous kind (ADR 0058)
        public void Reset()
        {
            Description = "";
            Kind = PlaytestReportKind.Bug;
            Generation++;
        }
    }
}
