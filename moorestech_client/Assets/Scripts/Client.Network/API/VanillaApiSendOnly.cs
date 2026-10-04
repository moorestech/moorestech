using System;
using System.Collections.Generic;
using Client.Network.API.Requests;
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
        public HotbarCommandApi Hotbar { get; }
        
        public VanillaApiSendOnly(PacketSender packetSender)
        {
            _packetSender = packetSender;
            Hotbar = new HotbarCommandApi(packetSender);
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
            var request = new PlaceBlockProtocol.SendPlaceBlockProtocolMessagePack(placePositions);
            _packetSender.Send(request);
        }

        public void SendPlayerPosition(Vector3 pos)
        {
            var request = new SetPlayerCoordinateProtocol.PlayerCoordinateSendProtocolMessagePack(pos);
            _packetSender.Send(request);
        }
        
        public void Craft(Guid craftRecipeId)
        {
            var request = new OneClickCraft.RequestOneClickCraftProtocolMessagePack(craftRecipeId);
            _packetSender.Send(request);
        }
        
        public void AttackMapObject(int instanceId)
        {
            var request = MiningProtocol.MiningProtocolMessagePack.CreateMapObjectRequest(instanceId);
            _packetSender.Send(request);
        }

        public void MineVein(Guid veinGuid, Vector3Int position)
        {
            var request = MiningProtocol.MiningProtocolMessagePack.CreateVeinRequest(veinGuid, position);
            _packetSender.Send(request);
        }
        
        /// <summary>
        /// 選択中の装備スロットをサーバーへ通知する（結果は装備更新イベントで返る）
        /// Notify the server of the selected equipment slot; the result comes back through the equipment update event
        /// </summary>
        public void SetSelectedEquipment(int selectedIndex)
        {
            var request = new SetSelectedEquipmentIndexProtocol.SetSelectedEquipmentIndexMessagePack(selectedIndex);
            _packetSender.Send(request);
        }

        public void SendCommand(string command)
        {
            var request = new SendCommandProtocol.SendCommandProtocolMessagePack(command);
            _packetSender.Send(request);
        }
        
        public void RegisterPlayedSkit(string skitId)
        {
            var request = new RegisterPlayedSkitProtocol.RegisterPlayedSkitMessagePack(skitId);
            _packetSender.Send(request);
        }
        
        public void RequestBlockState(Vector3Int position)
        {
            var request = new RequestBlockStateProtocol.RequestBlockStateProtocolMessagePack(position);
            _packetSender.Send(request);
        }
        
        public void CompleteBaseCamp(Vector3Int position)
        {
            var request = new CompleteBaseCampProtocol.CompleteBaseCampProtocolMessagePack(position);
            _packetSender.Send(request);
        }

        public void CompleteResearch(Guid researchGuid)
        {
            var request = new CompleteResearchProtocol.RequestCompleteResearchMessagePack(researchGuid);
            _packetSender.Send(request);
        }

        public void ConnectRail(int fromNodeId, Guid fromGuid, int toNodeId, Guid toGuid, Guid railTypeGuid)
        {
            var request = RailConnectionEditRequest.CreateConnectRequest(fromNodeId, fromGuid, toNodeId, toGuid, railTypeGuid);
            _packetSender.Send(request);
        }
        
        public void DisconnectRail(int fromNodeId, Guid fromGuid, int toNodeId, Guid toGuid)
        {
            var request = RailConnectionEditRequest.CreateDisconnectRequest(fromNodeId, fromGuid, toNodeId, toGuid);
            _packetSender.Send(request);
        }
        
        public void PlaceRailWithPier(int fromNodeId, Guid fromGuid, BlockId pierBlockId, PlaceInfo pierPlaceInfo, Guid railTypeGuid)
        {
            var request = RailConnectWithPlacePierProtocol.RailConnectWithPlacePierRequest.Create(fromNodeId, fromGuid, pierBlockId, pierPlaceInfo, railTypeGuid);
            _packetSender.Send(request);
        }
        
        public void SendTrainCarRidingInput(bool moveForward, bool moveBackward, bool selectPreviousBranch, bool selectNextBranch)
        {
            var request = new TrainCarRidingInputMessagePack(moveForward, moveBackward, selectPreviousBranch, selectNextBranch);
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
            var request = new SubscribeInventoryRequestMessagePack(identifier, isSubscribe);
            _packetSender.Send(request);
        }

        /// <summary>
        /// ギアチェーンポール間の接続を作成する
        /// Create a connection between GearChainPoles
        /// </summary>
        public void ConnectGearChain(Vector3Int posA, Vector3Int posB, Guid connectToolGuid)
        {
            var request = GearChainConnectionEditRequest.CreateConnectRequest(posA, posB, connectToolGuid);
            _packetSender.Send(request);
        }

        // ポール間の記録済み素材を返す切断を要求する
        // Request a disconnect that refunds the recorded pole connection materials
        public void DisconnectGearChain(Vector3Int posA, Vector3Int posB)
        {
            var request = GearChainConnectionEditRequest.CreateDisconnectRequest(posA, posB);
            _packetSender.Send(request);
        }

        /// <summary>
        /// 電気系ブロック間の電線を切断する
        /// Disconnect an electric wire between electric blocks
        /// </summary>
        public void DisconnectElectricWire(Vector3Int posA, Vector3Int posB)
        {
            var request = ElectricWireDisconnectProtocol.ElectricWireDisconnectRequest.CreateDisconnectRequest(posA, posB);
            _packetSender.Send(request);
        }

    }
}
