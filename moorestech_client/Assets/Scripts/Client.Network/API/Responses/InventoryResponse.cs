using System.Collections.Generic;
using Core.Item.Interface;
using Game.Block.Interface.State;
using Server.Protocol.PacketResponse;
using Server.Util.MessagePack;

namespace Client.Network.API
{
    public class InventoryResponse
    {
        public InventoryIdentifierMessagePack Identifier { get; }
        public List<IItemStack> Items { get; }
        public List<FluidMessagePack> Fluids { get; }
        public InventoryRequestResult Result { get; }

        public InventoryResponse(InventoryIdentifierMessagePack identifier, List<IItemStack> items, List<FluidMessagePack> fluids, InventoryRequestResult result)
        {
            Identifier = identifier;
            Items = items;
            Fluids = fluids;
            Result = result;
        }
    }

}
