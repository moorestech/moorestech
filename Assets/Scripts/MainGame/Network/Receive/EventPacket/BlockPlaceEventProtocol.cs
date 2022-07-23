using System.Collections.Generic;
using MainGame.Basic;
using MainGame.Network.Event;
using MainGame.Network.Util;
using MessagePack;
using Server.Event.EventReceive;
using UnityEngine;

namespace MainGame.Network.Receive.EventPacket
{
    public class BlockPlaceEventProtocol : IAnalysisEventPacket
    {
        readonly ReciveChunkDataEvent reciveChunkDataEvent;

        public BlockPlaceEventProtocol(ReciveChunkDataEvent reciveChunkDataEvent)
        {
            this.reciveChunkDataEvent = reciveChunkDataEvent;
        }

        public void Analysis(List<byte> packet)
        {
            
            var data = MessagePackSerializer
                .Deserialize<PlaceBlockEventMessagePack>(packet.ToArray());
            

            var direction = (BlockDirection)data.Direction;

            //ブロックをセットする
            reciveChunkDataEvent.InvokeBlockUpdateEvent(new BlockUpdateEventProperties(
                new Vector2Int(data.X,data.Y), data.BlockId,direction));
        }
    }
}