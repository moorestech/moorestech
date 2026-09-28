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
            var json = JsonConvert.SerializeObject(report, Formatting.Indented);

            // ディスク境界だけを隔離し、保存失敗をログに残す。
            // Isolate only the disk boundary and log diagnostic save failures.
            try
            {
                Directory.CreateDirectory(_directory);
                File.WriteAllText(path, json);
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
