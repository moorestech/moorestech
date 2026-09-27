using System;
using System.Collections.Generic;
using UnityEngine;

namespace Client.RemoteExec.Run
{
    // 実行中のUnityログを集める。同時刻の他処理のログも含む
    // Collect Unity logs during execution, including concurrent unrelated logs
    internal sealed class RemoteExecLogCapture : IDisposable
    {
        private readonly List<string> _lines = new();

        public RemoteExecLogCapture()
        {
            Application.logMessageReceivedThreaded += OnLog;
        }

        public List<string> TakeLines()
        {
            lock (_lines) return new List<string>(_lines);
        }

        public void Dispose()
        {
            Application.logMessageReceivedThreaded -= OnLog;
        }

        private void OnLog(string condition, string stackTrace, LogType type)
        {
            lock (_lines) _lines.Add($"[{type}] {condition}");
        }
    }
}
