using System;

namespace Client.Starter.PlaytestSmoke
{
    [Serializable]
    public sealed class StandalonePlaytestSmokeStep
    {
        public string name;
        public bool success;
        public string message;
        public float elapsedSeconds;
    }

    [Serializable]
    public sealed class StandalonePlaytestSmokeResult
    {
        public string phase;
        public bool success;
        public string message;
        public string reportBundleDirectory;
        // 送った報告の受け口側キー（SteamID）。検証スクリプトがACK状態に依らず存在確認に使う。報告を送らない段階では空
        // The receiver-side key (SteamID) of the sent report; the verify script checks existence with it regardless of ACK state. Empty when no report is sent
        public string reportSteamId;
        public StandalonePlaytestSmokeStep[] steps;
    }
}
