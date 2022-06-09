using System.Collections.Generic;
using MainGame.Basic;
using MainGame.Network.Event;
using MainGame.Network.Util;
using MessagePack;
using Server.Event.EventReceive;
using UnityEngine;

namespace MainGame.Network.Receive.EventPacket
{
    public class BlockRemoveEventProtocol : IAnalysisEventPacket 
    {
        private readonly NetworkReceivedChunkDataEvent _networkReceivedChunkDataEvents;

        public BlockRemoveEventProtocol(NetworkReceivedChunkDataEvent networkReceivedChunkDataEvents)
        {
            _networkReceivedChunkDataEvents = networkReceivedChunkDataEvents;
        }

        public void Analysis(List<byte> packet)
        {
            
            var data = MessagePackSerializer
                .Deserialize<RemoveBlockEventMessagePack>(packet.ToArray());
            
            
            _networkReceivedChunkDataEvents.InvokeBlockUpdateEvent(
                new BlockUpdateEventProperties(new Vector2Int(data.X,data.Y),BlockConstant.NullBlockId,BlockDirection.North));
        }
    }
}