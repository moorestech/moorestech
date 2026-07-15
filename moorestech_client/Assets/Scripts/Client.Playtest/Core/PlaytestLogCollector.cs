using System.Collections.Generic;
using UnityEngine;

namespace Client.Playtest.Core
{
    /// <summary>
    ///     シナリオ実行中のError/Exceptionログを収集して結果JSONに同梱する
    ///     Collects Error/Exception logs during a scenario run for inclusion in the result JSON
    /// </summary>
    public class PlaytestLogCollector
    {
        private readonly List<string> _errorLogs = new();
        public IReadOnlyList<string> ErrorLogs => _errorLogs;

        public void StartCollect()
        {
            Application.logMessageReceived += OnLogReceived;
        }

        public void StopCollect()
        {
            Application.logMessageReceived -= OnLogReceived;
        }

        private void OnLogReceived(string condition, string stackTrace, LogType type)
        {
            // エラー系のみ記録する（Warning/Logはノイズになるため除外）
            // Record only error-class logs (Warning/Log are excluded as noise)
            if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
            _errorLogs.Add($"[{type}] {condition}");
        }
    }
}
