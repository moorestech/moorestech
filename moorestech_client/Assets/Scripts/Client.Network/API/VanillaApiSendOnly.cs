using System;
using System.Collections.Generic;
using Client.Network.Settings;
using Core.Master;
using Game.CraftChainer.CraftChain;
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
            var request = new BlockInventoryOpenCloseProtocol.BlockInventoryOpenCloseProtocolMessagePack(_playerId, pos, isOpen);
            _packetSender.Send(request);
        }
        
        public void ItemMove(int count, ItemMoveType itemMoveType, ItemMoveInventoryInfo fromInv, int fromSlot, ItemMoveInventoryInfo toInv, int toSlot)
        {
            var request = new InventoryItemMoveProtocol.InventoryItemMoveProtocolMessagePack(_playerId, count, itemMoveType, fromInv, fromSlot, toInv, toSlot);
            _packetSender.Send(request);
        }
        
        public void PlaceHotBarBlock(List<PlaceInfo> placePositions, int hotBarSlot)
        {
            var request = new SendPlaceHotBarBlockProtocol.SendPlaceHotBarBlockProtocolMessagePack(_playerId, hotBarSlot, placePositions);
            _packetSender.Send(request);
        }
        
        public void BlockRemove(Vector3Int pos)
        {
            var request = new RemoveBlockProtocol.RemoveBlockProtocolMessagePack(_playerId, pos);
            _packetSender.Send(request);
        }
        
        public void SendPlayerPosition(Vector3 pos)
        {
            var request = new SetPlayerCoordinateProtocol.PlayerCoordinateSendProtocolMessagePack(_playerId, pos);
            _packetSender.Send(request);
        }
        
        public void Craft(Guid craftRecipeId)
        {
            var request = new OneClickCraft.RequestOneClickCraftProtocolMessagePack(_playerId, craftRecipeId);
            _packetSender.Send(request);
        }
        
        public void Save()
        {
            var request = new SaveProtocol.SaveProtocolMessagePack();
            _packetSender.Send(request);
        }
        
        public void AttackMapObject(int mapObjectInstanceId, int attackDamage)
        {
            var request = new MapObjectAcquisitionProtocol.GetMapObjectProtocolProtocolMessagePack(_playerId, mapObjectInstanceId, attackDamage);
            _packetSender.Send(request);
        }
        
        public void SetCraftChainerCrafterRecipe(Vector3Int block ,List<CraftingSolverItem> inputs, List<CraftingSolverItem> outputs)
        {
            var request = new SetCraftChainerCrafterRecipeProtocol.SetCraftChainerCrafterRecipeProtocolMessagePack(block, inputs, outputs);
            _packetSender.Send(request);
        }
        
        public void SetCraftChainerMainComputerRequestItem(Vector3Int block, ItemId itemId, int count)
        {
            var request = new SetCraftChainerMainComputerRequestItemProtocol.SetCraftChainerMainComputerRequestItemProtocolMessagePack(block, itemId, count);
            _packetSender.Send(request);
        }
        
        public void SendCommand(string command)
        {
            var request = new SendCommandProtocol.SendCommandProtocolMessagePack(command);
            _packetSender.Send(request);
        }
    }
}