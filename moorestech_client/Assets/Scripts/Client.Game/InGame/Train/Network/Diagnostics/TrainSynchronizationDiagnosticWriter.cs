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

        internal TrainSynchronizationDiagnosticWriteResult Write(TrainSynchronizationDiagnosticReport report)
        {
            var path = Path.Combine(_directory, $"stall-{report.CapturedAtUtc:yyyyMMddTHHmmssfffZ}-{Guid.NewGuid():N}.json");
            var json = JsonConvert.SerializeObject(report, Formatting.Indented);

            // ディスク境界だけを隔離し、失敗を結果とログの両方に残す。
            // Isolate only the disk boundary and preserve failures in both the result and log.
            try
            {
                Directory.CreateDirectory(_directory);
                File.WriteAllText(path, json);
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                var reason = $"[TrainSynchronization] Diagnostic save failed: {path}: {exception.Message}";
                Debug.LogError(reason);
                return new TrainSynchronizationDiagnosticWriteResult(reason);
            }
            Debug.Log($"[TrainSynchronization] Diagnostic saved: {path}");
            return new TrainSynchronizationDiagnosticWriteResult(null);
        }
    }

    internal sealed class TrainSynchronizationDiagnosticWriteResult
    {
        internal readonly string FailureReason;

        internal TrainSynchronizationDiagnosticWriteResult(string failureReason)
        {
            FailureReason = failureReason;
        }
    }
}
