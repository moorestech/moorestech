using System.Collections.Generic;
using Core.Item.Interface;
using Server.Protocol.PacketResponse;
using Server.Util.MessagePack;

namespace Client.Network.API
{
    public class InventoryResponse
    {
        public InventoryIdentifierMessagePack Identifier { get; }
        public List<IItemStack> Items { get; }
        public InventoryRequestResult Result { get; }

        public InventoryResponse(InventoryIdentifierMessagePack identifier, List<IItemStack> items, InventoryRequestResult result)
        {
            Identifier = identifier;
            Items = items;
            Result = result;
        }
    }
}
