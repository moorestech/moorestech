using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Game.World.Interface.DataStore;
using Constant;
using MainGame.Network.Event;
using MessagePack;
using Server.Event.EventReceive;
using UnityEngine;

namespace MainGame.Network.Receive.EventPacket
{
    public class BlockPlaceEventProtocol : IAnalysisEventPacket
    {
        private readonly ReceiveChunkDataEvent receiveChunkDataEvent;

        public BlockPlaceEventProtocol(ReceiveChunkDataEvent receiveChunkDataEvent)
        {
            this.receiveChunkDataEvent = receiveChunkDataEvent;
        }

        public void Analysis(List<byte> packet)
        {
            var data = MessagePackSerializer.Deserialize<PlaceBlockEventMessagePack>(packet.ToArray());


            var direction = (BlockDirection)data.Direction;

            //ブロックをセットする
            receiveChunkDataEvent.InvokeBlockUpdateEvent(new BlockUpdateEventProperties(new Vector2Int(data.X, data.Y), data.BlockId, direction)).Forget();
        }
    }
}