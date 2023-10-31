using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using MainGame.Basic;
using MainGame.Network.Event;

using MessagePack;
using Server.Protocol.PacketResponse;

namespace MainGame.Network.Receive
{
    public class ReceiveBlockInventoryProtocol : IAnalysisPacket
    {
        private readonly ReceiveBlockInventoryEvent receiveBlockInventoryEvent;

        public ReceiveBlockInventoryProtocol(ReceiveBlockInventoryEvent receiveBlockInventoryEvent)
        {
            this.receiveBlockInventoryEvent = receiveBlockInventoryEvent;
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
            
            receiveBlockInventoryEvent.InvokeSettingBlock(new SettingBlockInventoryProperties(items,data.BlockId)).Forget();
        }
    }
}