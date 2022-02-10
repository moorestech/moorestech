using System.Collections.Generic;
using MainGame.Basic;
using MainGame.Network.Event;
using MainGame.Network.Interface.Receive;
using MainGame.Network.Util;

namespace MainGame.Network.Receive
{
    public class ReceiveBlockInventoryProtocol : IAnalysisPacket
    {
        private readonly ReceiveBlockInventoryUpdateEvent _receiveBlockInventoryUpdateEvent;

        public ReceiveBlockInventoryProtocol(IReceiveBlockInventoryUpdateEvent receiveBlockInventoryUpdateEvent)
        {
            _receiveBlockInventoryUpdateEvent = receiveBlockInventoryUpdateEvent as ReceiveBlockInventoryUpdateEvent;
        }

        public void Analysis(List<byte> data)
        {
            var bytes = new ByteArrayEnumerator(data);
            bytes.MoveNextToGetShort(); //packet id
            var itemNum = bytes.MoveNextToGetShort();

            var items = new List<ItemStack>();
            for (int i = 0; i < itemNum; i++)
            {
                var id = bytes.MoveNextToGetInt();
                var count = bytes.MoveNextToGetShort();
                items.Add(new ItemStack(id, count));
            }
            
            bytes.MoveNextToGetShort();//UI type id 削除予定
            var inputNum = bytes.MoveNextToGetShort();
            var outputNum = bytes.MoveNextToGetShort();
            
            _receiveBlockInventoryUpdateEvent.OnOnSettingBlock(items,"",inputNum,outputNum);
        }
    }
}