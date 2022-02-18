using System.Collections.Generic;
using Core.Item;
using Game.PlayerInventory.Interface;
using Microsoft.Extensions.DependencyInjection;
using Server.Util;

namespace Server.Protocol.PacketResponse
{
    public class SendCommandProtocol : IPacketResponse
    {
        private readonly IPlayerInventoryDataStore _playerInventoryDataStore;
        private readonly ItemStackFactory _itemStackFactory;
        public SendCommandProtocol(ServiceProvider serviceProvider)
        {
            _playerInventoryDataStore = serviceProvider.GetService<IPlayerInventoryDataStore>();
            _itemStackFactory = serviceProvider.GetService<ItemStackFactory>();
        }

        public List<byte[]> GetResponse(List<byte> payload)
        {
            var enumerator = new ByteArrayEnumerator(payload);
            enumerator.MoveNextToGetShort();//packet id
            var length = enumerator.MoveNextToGetShort();//command length
            var command = enumerator.MoveNextToGetString(length).Split(' ');//command text
            
            //他のコマンドを実装する場合、この実装方法をやめる
            if (command[0] == "give")
            {
                var inventory = _playerInventoryDataStore.GetInventoryData(int.Parse(command[1]));
                var item = _itemStackFactory.Create(int.Parse(command[2]), int.Parse(command[3]));
                inventory.MainInventory.InsertItem(item);
            }
            
            
            return new List<byte[]>();
        }
    }
}