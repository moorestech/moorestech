using System;
using System.Collections.Generic;
using System.Linq;
using Game.PlayerInventory.Interface;
using Game.WorldMap;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using Server.Event;
using Server.Event.EventReceive;
using Server.Protocol.PacketResponse;
using UnityEngine;

namespace Server.Protocol
{
    public class PacketResponseCreator
    {
        private readonly Dictionary<string, IPacketResponse> _packetResponseDictionary = new();

        //TODO この辺もDIコンテナに載せる?こういうパケット周りめっちゃなんとかしたい
        public PacketResponseCreator(ServiceProvider serviceProvider)
        {
            _packetResponseDictionary.Add(InitialHandshakeProtocol.Tag, new InitialHandshakeProtocol(serviceProvider));
            _packetResponseDictionary.Add(RequestChunkDataProtocol.Tag, new RequestChunkDataProtocol(serviceProvider));
            _packetResponseDictionary.Add(PlayerInventoryResponseProtocol.Tag, new PlayerInventoryResponseProtocol(serviceProvider.GetService<IPlayerInventoryDataStore>()));
            _packetResponseDictionary.Add(SetPlayerCoordinateProtocol.Tag, new SetPlayerCoordinateProtocol(serviceProvider));
            _packetResponseDictionary.Add(EventProtocol.Tag, new EventProtocol(serviceProvider.GetService<EventProtocolProvider>()));
            _packetResponseDictionary.Add(InventoryItemMoveProtocol.Tag, new InventoryItemMoveProtocol(serviceProvider));
            _packetResponseDictionary.Add(SendPlaceHotBarBlockProtocol.Tag, new SendPlaceHotBarBlockProtocol(serviceProvider));
            _packetResponseDictionary.Add(BlockInventoryRequestProtocol.Tag, new BlockInventoryRequestProtocol(serviceProvider));
            _packetResponseDictionary.Add(RemoveBlockProtocol.Tag, new RemoveBlockProtocol(serviceProvider));
            _packetResponseDictionary.Add(SendCommandProtocol.Tag, new SendCommandProtocol(serviceProvider));
            _packetResponseDictionary.Add(MiningOperationProtocol.Tag, new MiningOperationProtocol(serviceProvider));
            _packetResponseDictionary.Add(BlockInventoryOpenCloseProtocol.Tag, new BlockInventoryOpenCloseProtocol(serviceProvider));
            _packetResponseDictionary.Add(SaveProtocol.Tag, new SaveProtocol(serviceProvider));
            _packetResponseDictionary.Add(GetMapObjectInfoProtocol.Tag, new GetMapObjectInfoProtocol(serviceProvider));
            _packetResponseDictionary.Add(MapObjectAcquisitionProtocol.Tag, new MapObjectAcquisitionProtocol(serviceProvider));
            _packetResponseDictionary.Add(OneClickCraft.Tag, new OneClickCraft(serviceProvider));

            serviceProvider.GetService<VeinGenerator>();
        }

        public List<List<byte>> GetPacketResponse(List<byte> payload)
        {
            var request = MessagePackSerializer.Deserialize<ProtocolMessagePackBase>(payload.ToArray());
            var response = _packetResponseDictionary[request.Tag].GetResponse(payload);
            
            if (response == null)
            {
                return new List<List<byte>>();
            }

            response.SequenceId = request.SequenceId;
            var responseBytes = MessagePackSerializer.Serialize(Convert.ChangeType(response, response.GetType()));
            
            return new List<List<byte>> {responseBytes.ToList()};
        }
    }
}