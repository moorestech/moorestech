using UnityEngine;

namespace Client.Ui.State.Diagnostics
{
    /// <summary>
    /// 同じ異常は継続中 1 回だけログへ出す旗。旗とログを 1 か所に持ち、呼び手は読みごとに Observe するだけ
    /// Flag that logs the same anomaly once while it persists; owns both the flag and the log so callers only Observe each reading
    /// </summary>
    public sealed class AnomalyReportGate
    {
        private readonly string _logPrefix;
        private readonly string _logSuffix;
        private readonly LogType _logType;
        private string _reportedAnomaly;

        public AnomalyReportGate(string logPrefix, string logSuffix, LogType logType)
        {
            _logPrefix = logPrefix;
            _logSuffix = logSuffix;
            _logType = logType;
        }

        // anomaly は今回の読みの異常の文面。null（異常なし・区画なし）で旗を戻し、新しい文面のときだけログへ出す
        // anomaly is this reading's text; null (healthy or absent) clears the flag, and only a new text is logged
        public void Observe(string anomaly)
        {
            if (!ShouldReport(anomaly)) return;
            Debug.unityLogger.Log(_logType, (object)$"{_logPrefix}{anomaly}{_logSuffix}");
        }

        private bool ShouldReport(string anomaly)
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
