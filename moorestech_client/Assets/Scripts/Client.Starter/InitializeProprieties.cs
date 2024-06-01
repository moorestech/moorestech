using System.Diagnostics;

namespace Client.Starter
{
    public class InitializeProprieties
    {
        public readonly bool IsLocal;
        public readonly Process LocalServerProcess;
        public readonly int PlayerId;
        public readonly string ServerIp;
        public readonly int ServerPort;

        public InitializeProprieties(bool isLocal, Process localServerProcess, string serverIp, int serverPort, int playerId)
        {
            IsLocal = isLocal;
            LocalServerProcess = localServerProcess;
            ServerIp = serverIp;
            ServerPort = serverPort;
            PlayerId = playerId;
        }
    }
}