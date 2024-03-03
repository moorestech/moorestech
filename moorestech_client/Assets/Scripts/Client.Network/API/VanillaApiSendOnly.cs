using System.Linq;
using Core.Item;
using Game.World.Interface.DataStore;
using MainGame.Network.Settings;
using MessagePack;
using Server.Protocol.PacketResponse;
using Server.Protocol.PacketResponse.Util.InventoryMoveUtil;
using UnityEngine;

namespace Client.Network.API
{
    public class VanillaApiSendOnly
    {
        private readonly PacketSender _packetSender;
        private readonly ItemStackFactory _itemStackFactory;
        private readonly PlayerConnectionSetting _playerConnectionSetting;
        private readonly int _playerId;
        
        public VanillaApiSendOnly(PacketSender packetSender, ItemStackFactory itemStackFactory, PlayerConnectionSetting playerConnectionSetting)
        {
            _packetSender = packetSender;
            _itemStackFactory = itemStackFactory;
            _playerConnectionSetting = playerConnectionSetting;
            _playerId = playerConnectionSetting.PlayerId;
        }
        
        public void SetOpenCloseBlock(Vector2Int pos, bool isOpen)
        {
            var request = new BlockInventoryOpenCloseProtocolMessagePack(_playerId, pos.x, pos.y, isOpen);
            _packetSender.Send(request);
        }
        
        public void ItemMove(int count, ItemMoveType itemMoveType, ItemMoveInventoryInfo fromInv,int fromSlot, ItemMoveInventoryInfo toInv,int toSlot)
        {
            var request = new InventoryItemMoveProtocolMessagePack(_playerId, count, itemMoveType, fromInv, fromSlot, toInv, toSlot);
            _packetSender.Send(request);
        }
        
        public void PlaceHotBarBlock(int x, int y, short hotBarSlot, BlockDirection blockDirection)
        {
            var request = new SendPlaceHotBarBlockProtocolMessagePack(_playerId, (int)blockDirection, hotBarSlot, x, y);
            _packetSender.Send(request);
        }
        
        public void BlockRemove(int x, int y)
        {
            var request = new RemoveBlockProtocolMessagePack(_playerId, x, y);
            _packetSender.Send(request);
        }
        
        public void SendPlayerPosition(Vector2 pos)
        {
            var request = new PlayerCoordinateSendProtocolMessagePack(_playerId, pos.x, pos.y);
            _packetSender.Send(request);
        }
        
        public void Craft(int craftRecipeId)
        {
            var request = new RequestOneClickCraftProtocolMessagePack(_playerId, craftRecipeId);
            _packetSender.Send(request);
        }

        public void Save()
        {
            var request = new SaveProtocolMessagePack();
            _packetSender.Send(request);
        }

        public void GetMapObject(int mapObjectInstanceId)
        {
            var request = new GetMapObjectProtocolProtocolMessagePack(_playerId, mapObjectInstanceId);
            _packetSender.Send(request);
        }
        
        public void SendCommand(string command)
        {
            var request = new SendCommandProtocolMessagePack(command);
            _packetSender.Send(request);
        }
    }
}