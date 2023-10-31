using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using MainGame.Network.Event;
using MessagePack;
using Server.Event.EventReceive;
using UnityEngine;

namespace MainGame.Network.Receive.EventPacket
{
    public class BlockStateChangeEventProtocol : IAnalysisEventPacket 
    {
        private readonly ReceiveBlockStateChangeEvent _receiveBlockStateChangeEvent;

        public BlockStateChangeEventProtocol(ReceiveBlockStateChangeEvent receiveBlockStateChangeEvent)
        {
            _receiveBlockStateChangeEvent = receiveBlockStateChangeEvent;
        }

        public void Analysis(List<byte> packet)
        {
            var data = MessagePackSerializer
                .Deserialize<ChangeBlockStateEventMessagePack>(packet.ToArray());
            

            _receiveBlockStateChangeEvent.InvokeReceiveBlockStateChange(new BlockStateChangeProperties(
                data.CurrentState,data.PreviousState,data.CurrentStateJsonData,data.Position)).Forget();
        }
    }
}