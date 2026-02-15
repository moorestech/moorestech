using System;
using System.Collections.Generic;
using Client.Network.Settings;
using Core.Master;

using Game.CraftTree.Models;
using Game.Train.RailPositions;
using Game.Train.Unit;
using Server.Protocol.PacketResponse;
using Server.Protocol.PacketResponse.Util.InventoryMoveUtil;
using Server.Util.MessagePack;
using UnityEngine;
using static Server.Protocol.PacketResponse.RailConnectionEditProtocol;
using static Server.Protocol.PacketResponse.PlaceTrainCarOnRailProtocol;
using static Server.Protocol.PacketResponse.SubscribeInventoryProtocol;
using static Server.Protocol.PacketResponse.GearChainConnectionEditProtocol;

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
        
        
        public void ItemMove(int count, ItemMoveType itemMoveType, ItemMoveInventoryInfo fromInv, int fromSlot, ItemMoveInventoryInfo toInv, int toSlot)
        {
            var request = new InventoryItemMoveProtocol.InventoryItemMoveProtocolMessagePack(_playerId, count, itemMoveType, fromInv, fromSlot, toInv, toSlot);
            _packetSender.Send(request);
        }
        
        public void PlaceHotBarBlock(List<PlaceInfo> placePositions, int hotBarSlot)
        {
            var request = new PlaceBlockFromHotBarProtocol.SendPlaceHotBarBlockProtocolMessagePack(_playerId, hotBarSlot, placePositions);
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
        
        public void SendCommand(string command)
        {
            var request = new SendCommandProtocol.SendCommandProtocolMessagePack(command);
            _packetSender.Send(request);
        }
        
        public void SendCraftTreeNode(Guid target ,List<CraftTreeNode> craftTree)
        {
            var request = new ApplyCraftTreeProtocol.ApplyCraftProtocolMessagePack(_playerId, target, craftTree);
            _packetSender.Send(request);
        }
        
        public void RegisterPlayedSkit(string skitId)
        {
            var request = new RegisterPlayedSkitProtocol.RegisterPlayedSkitMessagePack(_playerId, skitId);
            _packetSender.Send(request);
        }
        
        public void InvokeBlockState(Vector3Int position)
        {
            var request = new InvokeBlockStateEventProtocol.RequestInvokeBlockStateProtocolMessagePack(position);
            _packetSender.Send(request);
        }
        
        public void CompleteBaseCamp(Vector3Int position)
        {
            var request = new CompleteBaseCampProtocol.CompleteBaseCampProtocolMessagePack(_playerId, position);
            _packetSender.Send(request);
        }

        public void CompleteResearch(Guid researchGuid)
        {
            var request = new CompleteResearchProtocol.RequestCompleteResearchMessagePack(_playerId, researchGuid);
            _packetSender.Send(request);
        }

        public void ConnectRail(int fromNodeId, Guid fromGuid, int toNodeId, Guid toGuid, Guid railTypeGuid)
        {
            var request = RailConnectionEditRequest.CreateConnectRequest(_playerId, fromNodeId, fromGuid, toNodeId, toGuid, railTypeGuid);
            _packetSender.Send(request);
        }
        
        public void DisconnectRail(int fromNodeId, Guid fromGuid, int toNodeId, Guid toGuid)
        {
            var request = RailConnectionEditRequest.CreateDisconnectRequest(_playerId, fromNodeId, fromGuid, toNodeId, toGuid);
            _packetSender.Send(request);
        }
        
        public void PlaceRailWithPier(int fromNodeId, Guid fromGuid, int pierInventorySlot, PlaceInfo pierPlaceInfo, Guid railTypeGuid)
        {
            var request = RailConnectWithPlacePierProtocol.RailConnectWithPlacePierRequest.Create(_playerId, fromNodeId, fromGuid, pierInventorySlot, pierPlaceInfo, railTypeGuid);
            _packetSender.Send(request);
        }
        
        public void PlaceTrainOnRail(RailPositionSaveData railPosition, int hotBarSlot)
        {
            var railPositionSnapshot = new RailPositionSnapshotMessagePack(railPosition);
            var request = new PlaceTrainOnRailRequestMessagePack(railPositionSnapshot, hotBarSlot, _playerId);
            _packetSender.Send(request);
        }
        
        public void RemoveTrain(TrainCarInstanceId trainCarInstanceId)
        {
            var request = new RemoveTrainCarProtocol.RemoveTrainCarRequestMessagePack(trainCarInstanceId.AsPrimitive());
            _packetSender.Send(request);
        }
        
        /// <summary>
        /// インベントリをサブスクライブ/アンサブスクライブ
        /// Subscribe/Unsubscribe inventory
        /// </summary>
        public void SubscribeInventory(InventoryIdentifierMessagePack identifier, bool isSubscribe)
        {
            var request = new SubscribeInventoryRequestMessagePack(_playerId, identifier, isSubscribe);
            _packetSender.Send(request);
        }

        /// <summary>
        /// ギアチェーンポール間の接続を作成する
        /// Create a connection between GearChainPoles
        /// </summary>
        public void ConnectGearChain(Vector3Int posA, Vector3Int posB, ItemId itemId)
        {
            var request = GearChainConnectionEditRequest.CreateConnectRequest(posA, posB, _playerId, itemId);
            _packetSender.Send(request);
        }

        /// <summary>
        /// ギアチェーンポール間の接続を解除する
        /// Disconnect a connection between GearChainPoles
        /// </summary>
        public void DisconnectGearChain(Vector3Int posA, Vector3Int posB)
        {
            var request = GearChainConnectionEditRequest.CreateDisconnectRequest(posA, posB, _playerId);
            _packetSender.Send(request);
        }
    }
}
