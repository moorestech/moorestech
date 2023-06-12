using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using MainGame.Basic;
using MainGame.Network.Event;

using MessagePack;
using Server.Protocol.PacketResponse;

namespace MainGame.Network.Receive
{
    /// <summary>
    /// Analysis player inventory data 
    /// </summary>
    public class ReceivePlayerInventoryProtocol : IAnalysisPacket
    {
        private readonly ReceiveMainInventoryEvent receiveMainInventoryEvent;
        private readonly ReceiveCraftingInventoryEvent receiveCraftingInventoryEvent;
        private readonly ReceiveGrabInventoryEvent receiveGrabInventoryEvent;

        public ReceivePlayerInventoryProtocol(ReceiveMainInventoryEvent receiveMainInventoryEvent,ReceiveCraftingInventoryEvent receiveCraftingInventoryEvent,ReceiveGrabInventoryEvent receiveGrabInventoryEvent)
        {
            this.receiveMainInventoryEvent = receiveMainInventoryEvent;
            this.receiveCraftingInventoryEvent = receiveCraftingInventoryEvent;
            this.receiveGrabInventoryEvent = receiveGrabInventoryEvent;
        }


        public void Analysis(List<byte> packet)
        {

            var data = MessagePackSerializer.Deserialize<PlayerInventoryResponseProtocolMessagePack>(packet.ToArray());
            
            
            
            
            //main inventory items
            var mainItems = new List<ItemStack>();
            for (int i = 0; i < PlayerInventoryConstant.MainInventorySize; i++)
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
            
            
            
            
            //craft inventory items
            var craftItems = new List<ItemStack>();
            for (int i = 0; i < PlayerInventoryConstant.CraftingSlotSize; i++)
            {
                var item = data.Craft[i];
                craftItems.Add(new ItemStack(item.Id, item.Count));
            }
            var resultItem = new ItemStack(data.CraftResult.Id,data.CraftResult.Count);
            receiveCraftingInventoryEvent.InvokeCraftingInventoryUpdate(
                new CraftingInventoryUpdateProperties(data.PlayerId,data.IsCreatable,craftItems,resultItem)).Forget();
            
        }
    }
}