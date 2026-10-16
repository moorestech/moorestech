using System;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;

namespace Client.Game.InGame.Train.Network.Diagnostics
{
    public sealed class TrainSynchronizationDiagnosticWriter
    {
        private readonly string _directory;

        public TrainSynchronizationDiagnosticWriter(string directory)
        {
            _directory = directory;
        }

        internal void Write(TrainSynchronizationDiagnosticReport report)
        {
            var path = Path.Combine(_directory, $"stall-{report.CapturedAtUtc:yyyyMMddTHHmmssfffZ}-{Guid.NewGuid():N}.json");
            var temporary = path + ".tmp";
            var json = JsonConvert.SerializeObject(report, Formatting.Indented);

            // ディスク境界だけを隔離し、保存失敗をログに残す。
            // Isolate only the disk boundary and log diagnostic save failures.
            try
            {
                Directory.CreateDirectory(_directory);
                // コピー対象の*.jsonには書き終えた本文だけを公開する
                // Publish only a complete document under the copied *.json pattern
                File.WriteAllText(temporary, json);
                File.Move(temporary, path);
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                var reason = $"[TrainSynchronization] Diagnostic save failed: {path}: {exception.Message}";
                Debug.LogError(reason);
                return;
            }
            Debug.Log($"[TrainSynchronization] Diagnostic saved: {path}");
        }
    }
}
