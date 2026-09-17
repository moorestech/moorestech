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
        public StandalonePlaytestSmokeStep[] steps;
    }
}
