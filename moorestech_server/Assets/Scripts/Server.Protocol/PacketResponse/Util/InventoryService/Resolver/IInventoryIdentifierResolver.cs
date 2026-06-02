using Core.Inventory;
using Server.Protocol.PacketResponse.Util.InventoryMoveUtil;
using Server.Util.MessagePack;

namespace Server.Protocol.PacketResponse.Util.InventoryService.Resolver
{
    // WIP
    public interface IInventoryIdentifierResolver
    {
        ItemMoveInventoryType InventoryType { get; }
        
        IOpenableInventory Resolve(InventoryIdentifierMessagePack identifier);
    }
}