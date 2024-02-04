using System.Diagnostics;

namespace MainGame.Starter
{
    public class MainGameStartProprieties
    {
        public readonly bool isLocal;
        public readonly Process localServerProcess;
        public readonly int playerId;
        public readonly string serverIp;
        public readonly int serverPort;

        public MainGameStartProprieties(bool isLocal, Process localServerProcess, string serverIp, int serverPort, int playerId)
        {
            this.isLocal = isLocal;
            this.localServerProcess = localServerProcess;
            this.serverIp = serverIp;
            this.serverPort = serverPort;
            this.playerId = playerId;
        }
    }
}