using System;
using System.IO;
using UnityEngine;

namespace Client.Game.InGame.Train.Network.Diagnostics
{
    public static class TrainSynchronizationDiagnosticDirectory
    {
        public static DateTime PlayStartedAtUtc { get; private set; }

        // 今回の再生で書かれた診断だけを選ぶため、実世界の開始日時を記録する
        // Record the real-world play start so only diagnostics from this session are selected
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void RecordPlayStart()
        {
            PlayStartedAtUtc = DateTime.UtcNow;
        }

        public static string Resolve()
        {
            return Path.Combine(Application.persistentDataPath, "Logs", "TrainSynchronization");
        }
    }
}
