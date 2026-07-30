using System;

namespace Client.Starter.StandaloneQa
{
    [Serializable]
    public sealed class StandaloneTerrainQaResult
    {
        public bool success;
        public bool gameInitialized;
        public int terrainCount;
        public string[] invalidTerrainNames;
        public string[] shaderNames;
        public string screenshotPath;
        public long elapsedMilliseconds;
        public string message;
    }
}
