using System.Collections.Generic;
using Core.Item;
using MainGame.Network.Event;
using MainGame.Network.Receive;
using MainGame.UnityView.UI.Inventory;
using MainGame.UnityView.UI.Inventory.Main;
using MainGame.UnityView.UI.Inventory.Sub;
using MessagePack;
using Server.Event.EventReceive;
using Server.Protocol;
using Server.Protocol.PacketResponse;
using SinglePlay;
using UnityEngine;

namespace MainGame.Network
{
    public class AllReceivePacketAnalysisService
    {
        private readonly Dictionary<string, IAnalysisPacket> _analysisPackets = new();
        private int _packetCount;


        public AllReceivePacketAnalysisService(ReceiveChunkDataEvent receiveChunkDataEvent, BlockInventoryView blockInventoryView, ReceiveInitialHandshakeProtocol receiveInitialHandshakeProtocol, ReceiveEntitiesDataEvent receiveEntitiesDataEvent, ReceiveBlockStateChangeEvent receiveBlockStateChangeEvent, ReceiveUpdateMapObjectEvent receiveUpdateMapObjectEvent,
            LocalPlayerInventoryDataController localPlayerInventoryDataController, SinglePlayInterface singlePlayInterface,IInventoryItems inventoryItems)
        {
            var inventoryMainAndSubCombineItems = (InventoryMainAndSubCombineItems)inventoryItems;
            _analysisPackets.Add(DummyProtocol.Tag, new ReceiveDummyProtocol());
            _analysisPackets.Add(InitialHandshakeProtocol.Tag, receiveInitialHandshakeProtocol);
            _analysisPackets.Add(PlayerCoordinateSendProtocol.ChunkDataTag, new ReceiveChunkDataProtocol(receiveChunkDataEvent));
            _analysisPackets.Add(PlayerCoordinateSendProtocol.EntityDataTag, new ReceiveEntitiesProtocol(receiveEntitiesDataEvent));
            _analysisPackets.Add(EventProtocolMessagePackBase.EventProtocolTag, new ReceiveEventProtocol(receiveChunkDataEvent,blockInventoryView , receiveBlockStateChangeEvent, receiveUpdateMapObjectEvent,localPlayerInventoryDataController,singlePlayInterface.ItemStackFactory,inventoryMainAndSubCombineItems));
            _analysisPackets.Add(PlayerInventoryResponseProtocol.Tag, new ReceivePlayerInventoryProtocol(singlePlayInterface.ItemStackFactory,localPlayerInventoryDataController));
            _analysisPackets.Add(BlockInventoryRequestProtocol.Tag, new ReceiveBlockInventoryProtocol(blockInventoryView,singlePlayInterface));
            _analysisPackets.Add(MapObjectDestructionInformationProtocol.Tag, new ReceiveMapObjectDestructionInformationProtocol(receiveUpdateMapObjectEvent));
        }

        public void Analysis(List<byte> packet)
        {
            var tag = MessagePackSerializer.Deserialize<ProtocolMessagePackBase>(packet.ToArray()).Tag;

            //receive debug
            _packetCount++;
            if (!_analysisPackets.TryGetValue(tag, out var analyser))
            {
                Debug.LogError("Count " + _packetCount + " NotFoundTag " + tag);
                return;
            }


            //analysis packet
            analyser.Analysis(packet);
        }
    }
}