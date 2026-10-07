using System;
using System.Collections.Generic;
using UnityEngine;

namespace Client.RemoteExec.Run
{
    // 実行中のUnityログを集める。同時刻の他処理のログも含む
    // Collect Unity logs during execution, including concurrent unrelated logs
    internal sealed class RemoteExecLogCapture : IDisposable
    {
        private const int MaximumLines = 1000;
        private const int MaximumCharacters = 256 * 1024;
        private readonly List<string> _lines = new();
        private int _characters;
        private int _droppedLines;

        public RemoteExecLogCapture()
        {
            Application.logMessageReceivedThreaded += OnLog;
        }

        public List<string> TakeLines()
        {
            lock (_lines)
            {
                var result = new List<string>(_lines);
                if (0 < _droppedLines) result.Add($"[RemoteExec] ログ上限により {_droppedLines} 行を省略しました");
                return result;
            }
        }

        public void Dispose()
        {
            Application.logMessageReceivedThreaded -= OnLog;
        }

        private void OnLog(string condition, string stackTrace, LogType type)
        {
            // 長い待機や巨大な一行でも応答用メモリを際限なく増やさない
            // Bound response memory during long waits and oversized individual messages
            lock (_lines)
            {
                var prefix = $"[{type}] ";
                var remaining = MaximumCharacters - _characters;
                if (MaximumLines <= _lines.Count || remaining < condition.Length + prefix.Length)
                {
                    _droppedLines++;
                    return;
                }
                var line = prefix + condition;
                _lines.Add(line);
                _characters += line.Length;
            }
        }
    }
}
