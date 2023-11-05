using System.Collections.Generic;
using System.Linq;
using MainGame.Basic;
using MainGame.Network.Settings;
using MessagePack;
using Server.Protocol.PacketResponse;
using Server.Util.MessagePack;

namespace MainGame.Network.Send
{
    public class SendSetRecipeCraftingInventoryProtocol
    {
        private readonly int _playerId;
        private readonly ISocketSender _socketSender;

        public SendSetRecipeCraftingInventoryProtocol(ISocketSender socketSender, PlayerConnectionSetting playerConnectionSetting)
        {
            _socketSender = socketSender;
            _playerId = playerConnectionSetting.PlayerId;
        }

        public void Send(List<ItemStack> recipeItem)
        {
            var sendItem = new ItemMessagePack[PlayerInventoryConstant.CraftingSlotSize];
            for (var i = 0; i < sendItem.Length; i++) sendItem[i] = new ItemMessagePack(recipeItem[i].ID, recipeItem[i].Count);


            _socketSender.Send(MessagePackSerializer.Serialize(
                new SetRecipeCraftingInventoryProtocolMessagePack(_playerId, sendItem)
            ).ToList());
        }
    }
}