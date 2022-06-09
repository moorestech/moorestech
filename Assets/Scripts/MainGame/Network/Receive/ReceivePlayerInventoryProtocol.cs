using System.Collections.Generic;
using MainGame.Basic;
using MainGame.Network.Event;
using MainGame.Network.Util;
using MessagePack;
using Server.Protocol.PacketResponse;

namespace MainGame.Network.Receive
{
    /// <summary>
    /// Analysis player inventory data 
    /// </summary>
    public class ReceivePlayerInventoryProtocol : IAnalysisPacket
    {
        private readonly MainInventoryUpdateEvent _mainInventoryUpdateEvent;
        private readonly CraftingInventoryUpdateEvent _craftingInventoryUpdateEvent;
        private readonly GrabInventoryUpdateEvent _grabInventoryUpdateEvent;

        public ReceivePlayerInventoryProtocol(MainInventoryUpdateEvent mainInventoryUpdateEvent,CraftingInventoryUpdateEvent craftingInventoryUpdateEvent,GrabInventoryUpdateEvent grabInventoryUpdateEvent)
        {
            _mainInventoryUpdateEvent = mainInventoryUpdateEvent;
            _craftingInventoryUpdateEvent = craftingInventoryUpdateEvent;
            _grabInventoryUpdateEvent = grabInventoryUpdateEvent;
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
            _mainInventoryUpdateEvent.InvokeMainInventoryUpdate(
                new MainInventoryUpdateProperties(
                    data.PlayerId,
                    mainItems));
            
            
            
            
            //grab inventory items
            var grabItem = new ItemStack(data.Grab.Id, data.Grab.Count);
            _grabInventoryUpdateEvent.GrabInventoryUpdateEventInvoke(new GrabInventoryUpdateEventProperties(grabItem));
            
            
            
            
            //craft inventory items
            var craftItems = new List<ItemStack>();
            for (int i = 0; i < PlayerInventoryConstant.CraftingSlotSize; i++)
            {
                var item = data.Craft[i];
                craftItems.Add(new ItemStack(item.Id, item.Count));
            }
            var resultItem = new ItemStack(data.CraftResult.Id,data.CraftResult.Count);
            _craftingInventoryUpdateEvent.InvokeCraftingInventoryUpdate(
                new CraftingInventoryUpdateProperties(data.PlayerId,data.IsCreatable,craftItems,resultItem));
            
        }
    }
}