using System.Collections.Generic;
using System.Linq;
using MainGame.Network.Settings;
using MainGame.Network.Util;
using MessagePack;
using Server.Protocol.PacketResponse;
using UnityEngine;

namespace MainGame.Network.Send
{
    public class SendMiningProtocol
    {
        private readonly ISocket _socket;
        private const short ProtocolId = 15;
        private readonly int _playerId;
        
        public SendMiningProtocol(PlayerConnectionSetting playerConnectionSetting,ISocket socket)
        {
            _socket = socket;
            _playerId = playerConnectionSetting.PlayerId;
        }
        
        public void Send(Vector2Int pos)
        {
            _socket.Send(MessagePackSerializer.Serialize(new MiningOperationProtocolMessagePack(
                _playerId,pos.x,pos.y)).ToList());
        }
    }
}