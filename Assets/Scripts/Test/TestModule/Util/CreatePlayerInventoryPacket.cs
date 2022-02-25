using System.Collections.Generic;
using MainGame.Basic;
using MainGame.Network.Util;

namespace Test.TestModule.Util
{
    public class CreatePlayerInventoryPacket
    {
        
        //アイテムからプレイヤーインベントリのパケットを作る
        public static List<byte> Create(int playerId,Dictionary<int, ItemStack> items)
        {
            var packet = new List<byte>();
            packet.AddRange(ToByteList.Convert((short)4));
            packet.AddRange(ToByteList.Convert(playerId));
            packet.AddRange(ToByteList.Convert((short)0));

            for (int i = 0; i < PlayerInventoryConstant.MainInventorySize; i++)
            {
                if (items.ContainsKey(i))
                {
                    packet.AddRange(ToByteList.Convert(items[i].ID));   
                    packet.AddRange(ToByteList.Convert(items[i].Count));   
                    continue;
                }
                packet.AddRange(ToByteList.Convert(ItemConstant.NullItemId));   
                packet.AddRange(ToByteList.Convert(ItemConstant.NullItemCount));   
            }

            return packet;
        }
    }
}