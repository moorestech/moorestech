using System.Diagnostics;
using Client.Common;

namespace Client.Starter
{
    public class InitializeProprieties
    {
        public readonly Process LocalServerProcess;
        public readonly int PlayerId;
        public readonly string ServerIp;
        public readonly int ServerPort;
        
        public string LocalSaveFilePath { get; set; }
        
        public InitializeProprieties(Process localServerProcess, string serverIp, int serverPort, int playerId)
        {
            LocalServerProcess = localServerProcess;
            ServerIp = serverIp;
            ServerPort = serverPort;
            PlayerId = playerId;
        }
        
        public static InitializeProprieties CreateDefault()
        {
            var proprieties = new InitializeProprieties(null, ServerConst.LocalServerIp, ServerConst.LocalServerPort, ServerConst.DefaultPlayerId);
             
             return proprieties;
        }
    }
}