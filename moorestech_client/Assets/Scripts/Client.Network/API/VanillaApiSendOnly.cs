using Server.Protocol.PacketResponse.Rail;
using System;
using System.Collections.Generic;
using Client.Network.API.Requests;
using Core.Master;

using Game.Train.RailPositions;
using Game.Train.SaveLoad;
using Game.Train.Unit;
using Server.Protocol.PacketResponse;
using Server.Protocol.PacketResponse.Util.InventoryMoveUtil;
using Server.Util.MessagePack;
using UnityEngine;
using static Server.Protocol.PacketResponse.RailConnectionEditProtocol;
using static Server.Protocol.PacketResponse.SubscribeInventoryProtocol;
using static Server.Protocol.PacketResponse.TrainCarRidingInputProtocol;

namespace Client.Network.API
{
    public class VanillaApiSendOnly
    {
        private readonly PacketSender _packetSender;
        public HotbarCommandApi Hotbar { get; }
        public ConnectionLineCommandApi ConnectionLine { get; }
        
        public VanillaApiSendOnly(PacketSender packetSender)
        {
            _packetSender = packetSender;
            Hotbar = new HotbarCommandApi(packetSender);
            ConnectionLine = new ConnectionLineCommandApi(packetSender);
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
        
        public void PlaceBlock(List<PlaceInfo> placePositions, BlockPlacementWiring wiring)
        {
            var request = new PlaceBlockProtocol.SendPlaceBlockProtocolMessagePack(placePositions, wiring);
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
        
        // 再設置後の端点を座標で指定して同じ種類のレールを復元する
        // Restore the same rail type by identifying re-placed endpoints by position
        public void ConnectRailByDestination(ConnectionDestination from, ConnectionDestination to, Guid connectToolGuid)
        {
            var request = new RailConnectByDestinationProtocol.RailConnectByDestinationRequest(from, to, connectToolGuid);
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

    }
}
