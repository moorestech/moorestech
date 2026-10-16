using System;
using System.IO;
using UnityEngine;

namespace Client.Game.InGame.Train.Network.Diagnostics
{
    public static class TrainSynchronizationDiagnosticDirectory
    {
        private static string _playSessionId = Guid.NewGuid().ToString("N");

        // 今回の再生で書かれた診断だけを選ぶため、再生IDを更新する
        // Renew the play ID so only diagnostics from this session are selected
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void RecordPlayStart()
        {
            _playSessionId = Guid.NewGuid().ToString("N");
        }

        public static string Resolve()
        {
            return Path.Combine(Application.persistentDataPath, "Logs", "TrainSynchronization", _playSessionId);
        }
    }
}
