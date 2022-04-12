using System.Diagnostics;

namespace MainGame.Network.Settings
{
    public class ServerProcessSetting
    {
        public readonly bool isLocal;
        public readonly Process localServerProcess;

        public ServerProcessSetting(bool isLocal, Process localServerProcess)
        {
            this.isLocal = isLocal;
            this.localServerProcess = localServerProcess;
        }
    }
}