using System.Collections.Generic;
using MainGame.Basic;
using MainGame.Network.Event;
using MainGame.Network.Util;
using MessagePack;
using Server.Protocol.PacketResponse;

namespace MainGame.Network.Receive
{
    public class ReceiveBlockInventoryProtocol : IAnalysisPacket
    {
        private readonly BlockInventoryUpdateEvent _blockInventoryUpdateEvent;

        public ReceiveBlockInventoryProtocol(BlockInventoryUpdateEvent blockInventoryUpdateEvent)
        {
            _blockInventoryUpdateEvent = blockInventoryUpdateEvent;
        }

        public void Analysis(List<byte> packet)
        {
            var data = MessagePackSerializer.Deserialize<BlockInventoryResponseProtocolMessagePack>(packet.ToArray()); 
            

            var items = new List<ItemStack>();
            for (int i = 0; i < data.ItemCounts.Length; i++)
            {
                var id = data.ItemIds[i];
                var count = data.ItemCounts[i];
                items.Add(new ItemStack(id, count));
            }
            
            _blockInventoryUpdateEvent.InvokeSettingBlock(items,data.BlockId);
        }
    }
}