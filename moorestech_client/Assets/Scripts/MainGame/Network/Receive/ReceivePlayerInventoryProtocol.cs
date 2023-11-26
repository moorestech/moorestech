using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using MainGame.Basic;
using MainGame.Network.Event;
using MessagePack;
using Server.Protocol.PacketResponse;

namespace MainGame.Network.Receive
{
    /// <summary>
    ///     Analysis player inventory data
    /// </summary>
    public class ReceivePlayerInventoryProtocol : IAnalysisPacket
    {
        private readonly ReceiveGrabInventoryEvent receiveGrabInventoryEvent;
        private readonly ReceiveMainInventoryEvent receiveMainInventoryEvent;

        public ReceivePlayerInventoryProtocol(ReceiveMainInventoryEvent receiveMainInventoryEvent, ReceiveGrabInventoryEvent receiveGrabInventoryEvent)
        {
            this.receiveMainInventoryEvent = receiveMainInventoryEvent;
            this.receiveGrabInventoryEvent = receiveGrabInventoryEvent;
        }


        public void Analysis(List<byte> packet)
        {
            var data = MessagePackSerializer.Deserialize<PlayerInventoryResponseProtocolMessagePack>(packet.ToArray());


            //main inventory items
            var mainItems = new List<ItemStack>();
            for (var i = 0; i < PlayerInventoryConstant.MainInventorySize; i++)
            {
                var item = data.Main[i];
                mainItems.Add(new ItemStack(item.Id, item.Count));
            }

            receiveMainInventoryEvent.InvokeMainInventoryUpdate(
                new MainInventoryUpdateProperties(
                    data.PlayerId,
                    mainItems)).Forget();


            //grab inventory items
            var grabItem = new ItemStack(data.Grab.Id, data.Grab.Count);
            receiveGrabInventoryEvent.OnGrabInventoryUpdateEventInvoke(new GrabInventoryUpdateEventProperties(grabItem)).Forget();

        }
    }
}