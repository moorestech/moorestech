using System.Collections.Generic;
using MainGame.Basic;
using MainGame.Network.Event;
using MainGame.Network.Util;

namespace MainGame.Model.Network.Receive
{
    public class ReceiveBlockInventoryProtocol : IAnalysisPacket
    {
        private readonly BlockInventoryUpdateEvent _blockInventoryUpdateEvent;

        public ReceiveBlockInventoryProtocol(BlockInventoryUpdateEvent blockInventoryUpdateEvent)
        {
            _blockInventoryUpdateEvent = blockInventoryUpdateEvent;
        }

        public void Analysis(List<byte> data)
        {
            var bytes = new ByteArrayEnumerator(data);
            bytes.MoveNextToGetShort(); //packet id
            var itemNum = bytes.MoveNextToGetShort();
            
            var blockId = bytes.MoveNextToGetInt();

            var items = new List<ItemStack>();
            for (int i = 0; i < itemNum; i++)
            {
                var id = bytes.MoveNextToGetInt();
                var count = bytes.MoveNextToGetInt();
                items.Add(new ItemStack(id, count));
            }
            
            bytes.MoveNextToGetShort();//UI type id 削除予定
            var inputNum = bytes.MoveNextToGetShort();
            var outputNum = bytes.MoveNextToGetShort();
            
            _blockInventoryUpdateEvent.InvokeSettingBlock(items,"",blockId,inputNum,outputNum);
        }
    }
}