using System;
using System.Collections.Generic;
using MessagePack;
using Server.Util.MessagePack;

namespace Server.Protocol.PacketResponse
{
    public class SetRecipeCraftingInventoryProtocol: IPacketResponse
    {
        public const string Tag = "va:setRecipeCraftingInventory";
        public List<List<byte>> GetResponse(List<byte> payload)
        {
            throw new System.NotImplementedException();
        }
    }
    
    [MessagePackObject(keyAsPropertyName :true)]
    public class SetRecipeCraftingInventoryProtocolMessagePack : ProtocolMessagePackBase
    {
        public SetRecipeCraftingInventoryProtocolMessagePack(ItemMessagePack[] recipe)
        {
            Tag = SetRecipeCraftingInventoryProtocol.Tag;
            Recipe = recipe;
        }
        
        [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
        public SetRecipeCraftingInventoryProtocolMessagePack() { }

        public ItemMessagePack[] Recipe { get; set; }

    }
}