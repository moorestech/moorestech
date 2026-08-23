using System;
using System.Collections.Generic;
using Client.Network.Settings;
using Core.Master;

using Game.Train.RailPositions;
using Game.Train.Unit;
using Server.Protocol.PacketResponse;
using Server.Protocol.PacketResponse.Util.InventoryMoveUtil;
using Server.Util.MessagePack;
using UnityEngine;
using static Server.Protocol.PacketResponse.RailConnectionEditProtocol;
using static Server.Protocol.PacketResponse.SubscribeInventoryProtocol;
using static Server.Protocol.PacketResponse.GearChainConnectionEditProtocol;
using static Server.Protocol.PacketResponse.TrainCarRidingInputProtocol;

namespace Client.Network.API
{
    public class VanillaApiSendOnly
    {
        private readonly PacketSender _packetSender;
        private readonly int _playerId;
        
        public VanillaApiSendOnly(PacketSender packetSender, PlayerConnectionSetting playerConnectionSetting)
        {
            _packetSender = packetSender;
            _playerId = playerConnectionSetting.PlayerId;
        }
        
        
        public void ItemMove(int count, ItemMoveType itemMoveType, InventoryIdentifierMessagePack fromInv, int fromSlot, InventoryIdentifierMessagePack toInv, int toSlot)
        {
            var request = new InventoryItemMoveProtocol.InventoryItemMoveProtocolMessagePack(count, itemMoveType, fromInv, fromSlot, toInv, toSlot);
            _packetSender.Send(request);
        }

        public void SortInventory(InventoryIdentifierMessagePack target)
        {
            var request = new SortInventoryProtocol.SortInventoryProtocolMessagePack(target);
            _packetSender.Send(request);
        }
        
        public void PlaceBlock(List<PlaceInfo> placePositions)
        {
            var request = new PlaceBlockProtocol.SendPlaceBlockProtocolMessagePack(_playerId, placePositions);
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
        
        public void AttackMapObject(int instanceId)
        {
            var request = MiningProtocol.MiningProtocolMessagePack.CreateMapObjectRequest(_playerId, instanceId);
            _packetSender.Send(request);
        }

        public void MineVein(Guid veinGuid, Vector3Int position)
        {
            var request = MiningProtocol.MiningProtocolMessagePack.CreateVeinRequest(_playerId, veinGuid, position);
            _packetSender.Send(request);
        }
        
        /// <summary>
        /// 選択中の装備スロットをサーバーへ通知する（結果は装備更新イベントで返る）
        /// Notify the server of the selected equipment slot; the result comes back through the equipment update event
        /// </summary>
        public void SetSelectedEquipment(int selectedIndex)
        {
            var request = new SetSelectedEquipmentIndexProtocol.SetSelectedEquipmentIndexMessagePack(_playerId, selectedIndex);
            _packetSender.Send(request);
        }

        public void SendCommand(string command)
        {
            var request = new SendCommandProtocol.SendCommandProtocolMessagePack(command);
            _packetSender.Send(request);
        }
        
        public void RegisterPlayedSkit(string skitId)
        {
            var request = new RegisterPlayedSkitProtocol.RegisterPlayedSkitMessagePack(_playerId, skitId);
            _packetSender.Send(request);
        }
        
        public void RequestBlockState(Vector3Int position)
        {
            var request = new RequestBlockStateProtocol.RequestBlockStateProtocolMessagePack(position);
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
        
        public void PlaceRailWithPier(int fromNodeId, Guid fromGuid, BlockId pierBlockId, PlaceInfo pierPlaceInfo, Guid railTypeGuid)
        {
            var request = RailConnectWithPlacePierProtocol.RailConnectWithPlacePierRequest.Create(_playerId, fromNodeId, fromGuid, pierBlockId, pierPlaceInfo, railTypeGuid);
            _packetSender.Send(request);
        }
        
        public void SendTrainCarRidingInput(bool moveForward, bool moveBackward, bool selectPreviousBranch, bool selectNextBranch)
        {
            var request = new TrainCarRidingInputMessagePack(_playerId, moveForward, moveBackward, selectPreviousBranch, selectNextBranch);
            _packetSender.Send(request);
        }
        
        public void RemoveTrain(TrainCarInstanceId trainCarInstanceId)
        {
            var request = new RemoveTrainCarProtocol.RemoveTrainCarRequestMessagePack(trainCarInstanceId.AsPrimitive(), _playerId);
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
        public void ConnectGearChain(Vector3Int posA, Vector3Int posB, Guid connectToolGuid)
        {
            var request = GearChainConnectionEditRequest.CreateConnectRequest(posA, posB, _playerId, connectToolGuid);
            _packetSender.Send(request);
        }

        /// <summary>
        /// 電気系ブロック間の電線を切断する
        /// Disconnect an electric wire between electric blocks
        /// </summary>
        public void DisconnectElectricWire(Vector3Int posA, Vector3Int posB)
        {
            var request = ElectricWireDisconnectProtocol.ElectricWireDisconnectRequest.CreateDisconnectRequest(posA, posB, _playerId);
            _packetSender.Send(request);
        }

        /// <summary>
        /// ホットバーの枠へ設置対象を割り当てる（結果はホットバー更新イベントで返る）
        /// Assign a placement target to a hotbar slot; the result comes back through the hotbar update event
        /// </summary>
        public void AssignHotbar(int slot, Guid targetId)
        {
            var request = HotbarProtocol.HotbarProtocolMessagePack.CreateAssignRequest(_playerId, slot, targetId);
            _packetSender.Send(request);
        }

        /// <summary>
        /// ホットバーの枠を空にする
        /// Clear a hotbar slot
        /// </summary>
        public void ClearHotbar(int slot)
        {
            var request = HotbarProtocol.HotbarProtocolMessagePack.CreateClearRequest(_playerId, slot);
            _packetSender.Send(request);
        }

        /// <summary>
        /// ホットバーの2枠を入れ替える
        /// Swap two hotbar slots
        /// </summary>
        public void SwapHotbar(int slotA, int slotB)
        {
            var request = HotbarProtocol.HotbarProtocolMessagePack.CreateSwapRequest(_playerId, slotA, slotB);
            _packetSender.Send(request);
        }
    }
}
