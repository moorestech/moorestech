using Core.Inventory;
using Server.Util.MessagePack;

namespace Server.Protocol.PacketResponse.Util.InventoryService.Resolver
{
    public interface IInventoryIdentifierResolver
    {
        InventoryType InventoryType { get; }
        
        IOpenableInventory Resolve(InventoryIdentifierMessagePack identifier);
    }
}
