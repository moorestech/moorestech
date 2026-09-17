using Game.SaveLoad.Interface;

namespace Game.SaveLoad.Pruning
{
    /// <summary>ロード時に1度だけ書かれる除去件数の置き場。ロード前は除去なしとして読める</summary>
    /// <summary>Holds the prune counts written once at load; before the load it reads as "nothing removed"</summary>
    public sealed class MissingMasterPruneReportStore : IMissingMasterPruneReportLookup
    {
        public MissingMasterPruneReport Report { get; private set; } = MissingMasterPruneReport.None;

        public void SetReport(MissingMasterPruneReport report)
        {
            Report = report;
        }
    }
}
