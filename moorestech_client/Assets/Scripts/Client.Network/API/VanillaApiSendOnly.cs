using System.Collections.Generic;
using Client.Network.Settings;
using Server.Protocol.PacketResponse;
using Server.Protocol.PacketResponse.Util.InventoryMoveUtil;
using UnityEngine;

namespace Client.Network.API
{
    public class VanillaApiSendOnly
    {
        private readonly PacketSender _packetSender;
        private readonly PlayerConnectionSetting _playerConnectionSetting;
        private readonly int _playerId;
        
        public VanillaApiSendOnly(PacketSender packetSender, PlayerConnectionSetting playerConnectionSetting)
        {
            _packetSender = packetSender;
            _playerConnectionSetting = playerConnectionSetting;
            _playerId = playerConnectionSetting.PlayerId;
        }
        
        public void SetOpenCloseBlock(Vector3Int pos, bool isOpen)
        {
            var request = new BlockInventoryOpenCloseProtocolMessagePack(_playerId, pos, isOpen);
            _packetSender.Send(request);
        }
        
        public void ItemMove(int count, ItemMoveType itemMoveType, ItemMoveInventoryInfo fromInv, int fromSlot, ItemMoveInventoryInfo toInv, int toSlot)
        {
            var request = new InventoryItemMoveProtocolMessagePack(_playerId, count, itemMoveType, fromInv, fromSlot, toInv, toSlot);
            _packetSender.Send(request);
        }
        
        public void PlaceHotBarBlock(List<PlaceInfo> placePositions, int hotBarSlot)
        {
            var request = new SendPlaceHotBarBlockProtocolMessagePack(_playerId, hotBarSlot, placePositions);
            _packetSender.Send(request);
        }
        
        public void BlockRemove(Vector3Int pos)
        {
            var request = new RemoveBlockProtocolMessagePack(_playerId, pos);
            _packetSender.Send(request);
        }
        
        public void SendPlayerPosition(Vector2 pos)
        {
            var request = new PlayerCoordinateSendProtocolMessagePack(_playerId, pos);
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
        
        public void AttackMapObject(int mapObjectInstanceId, int attackDamage)
        {
            var request = new GetMapObjectProtocolProtocolMessagePack(_playerId, mapObjectInstanceId, attackDamage);
            _packetSender.Send(request);
        }
        
        public void SendCommand(string command)
        {
            var request = new SendCommandProtocolMessagePack(command);
            _packetSender.Send(request);
        }
    }
}