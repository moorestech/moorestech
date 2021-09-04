using System.Collections.Generic;
using industrialization.Core.Block;
using industrialization.OverallManagement.DataStore;
using industrialization.Server.Player;
using industrialization.Server.Util;

namespace industrialization.Server.PacketHandle.PacketResponse.ProtocolImplementation
{
    /// <summary>
    /// プレイヤー座標のプロトコル
    /// </summary>
    public class PlayerCoordinateSendProtocol
    {
        public List<byte[]> GetResponse(byte[] payload)
        {
            
            return new List<byte[]>();
        }

        private static PlayerCoordinateSendProtocol _instance;
        public static PlayerCoordinateSendProtocol Instance
        {
            get
            {
                if (_instance is null) _instance = new PlayerCoordinateSendProtocol();
                return _instance;
            }
        }
    }
}