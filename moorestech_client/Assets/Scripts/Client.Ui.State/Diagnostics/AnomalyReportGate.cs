namespace Client.Ui.State.Diagnostics
{
    /// <summary>
    /// 同じ異常は 1 回だけ報告させる旗
    /// Flag that reports the same anomaly once
    /// </summary>
    public sealed class AnomalyReportGate
    {
        private string _reportedAnomaly;

        // 異常なしの読みで旗を戻す。次の異常がまた報告される
        // Clears the flag on a healthy reading so the next anomaly is reported again
        public void Clear()
        {
            _reportedAnomaly = null;
        }

        // anomaly は異常の文面。null で旗を戻す
        // anomaly is the text; null clears the flag
        public bool ShouldReport(string anomaly)
        {
            if (anomaly == null)
            {
                Clear();
                return false;
            }
            if (anomaly == _reportedAnomaly) return false;
            _reportedAnomaly = anomaly;
            return true;
        }
    }
}
