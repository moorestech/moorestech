namespace Client.Ui.State.Diagnostics
{
    /// <summary>
    /// 同じ異常の間は 1 回だけ報告させる旗。異常なしで旗が戻り、次の異常でまた報告される
    /// A flag that lets the same anomaly be reported once; a healthy reading clears it so the next anomaly is reported again
    /// </summary>
    public sealed class AnomalyReportGate
    {
        private string _reportedAnomaly;

        // anomaly は異常の文面（無ければ null）。前回と同じ文面なら false
        // anomaly is the anomaly text (null when healthy); returns false when it equals the previous one
        public bool ShouldReport(string anomaly)
        {
            if (anomaly == null)
            {
                _reportedAnomaly = null;
                return false;
            }
            if (anomaly == _reportedAnomaly) return false;
            _reportedAnomaly = anomaly;
            return true;
        }
    }
}
