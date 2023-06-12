using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using MainGame.Basic;
using MainGame.Network.Event;

using MessagePack;
using Server.Event.EventReceive;
using UnityEngine;

namespace MainGame.Network.Receive.EventPacket
{
    public class BlockRemoveEventProtocol : IAnalysisEventPacket 
    {
        private readonly ReceiveChunkDataEvent receiveChunkDataEvents;

        public BlockRemoveEventProtocol(ReceiveChunkDataEvent receiveChunkDataEvents)
        {
            this.receiveChunkDataEvents = receiveChunkDataEvents;
        }

        public void Analysis(List<byte> packet)
        {
            
            var data = MessagePackSerializer
                .Deserialize<RemoveBlockEventMessagePack>(packet.ToArray());
            
            
            receiveChunkDataEvents.InvokeBlockUpdateEvent(
                new BlockUpdateEventProperties(new Vector2Int(data.X,data.Y),BlockConstant.NullBlockId,BlockDirection.North)).Forget();
        }
    }
}