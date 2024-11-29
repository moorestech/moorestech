using System;
using System.Collections.Generic;
using System.Linq;
using Game.PlayerInventory.Interface;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using Server.Event;
using Server.Protocol.PacketResponse;

namespace Server.Protocol
{
    public class PacketResponseCreator
    {
        private readonly Dictionary<string, IPacketResponse> _packetResponseDictionary = new();
        
        //TODO この辺もDIコンテナに載せる?こういうパケット周りめっちゃなんとかしたい
        public PacketResponseCreator(ServiceProvider serviceProvider)
        {
            _packetResponseDictionary.Add(InitialHandshakeProtocol.ProtocolTag, new InitialHandshakeProtocol(serviceProvider));
            _packetResponseDictionary.Add(RequestWorldDataProtocol.ProtocolTag, new RequestWorldDataProtocol(serviceProvider));
            _packetResponseDictionary.Add(PlayerInventoryResponseProtocol.ProtocolTag, new PlayerInventoryResponseProtocol(serviceProvider));
            _packetResponseDictionary.Add(SetPlayerCoordinateProtocol.ProtocolTag, new SetPlayerCoordinateProtocol(serviceProvider));
            _packetResponseDictionary.Add(EventProtocol.ProtocolTag, new EventProtocol(serviceProvider.GetService<EventProtocolProvider>()));
            _packetResponseDictionary.Add(InventoryItemMoveProtocol.ProtocolTag, new InventoryItemMoveProtocol(serviceProvider));
            _packetResponseDictionary.Add(SendPlaceHotBarBlockProtocol.ProtocolTag, new SendPlaceHotBarBlockProtocol(serviceProvider));
            _packetResponseDictionary.Add(BlockInventoryRequestProtocol.ProtocolTag, new BlockInventoryRequestProtocol(serviceProvider));
            _packetResponseDictionary.Add(RemoveBlockProtocol.ProtocolTag, new RemoveBlockProtocol(serviceProvider));
            _packetResponseDictionary.Add(SendCommandProtocol.ProtocolTag, new SendCommandProtocol(serviceProvider));
            _packetResponseDictionary.Add(BlockInventoryOpenCloseProtocol.ProtocolTag, new BlockInventoryOpenCloseProtocol(serviceProvider));
            _packetResponseDictionary.Add(SaveProtocol.ProtocolTag, new SaveProtocol(serviceProvider));
            _packetResponseDictionary.Add(GetMapObjectInfoProtocol.ProtocolTag, new GetMapObjectInfoProtocol(serviceProvider));
            _packetResponseDictionary.Add(MapObjectAcquisitionProtocol.ProtocolTag, new MapObjectAcquisitionProtocol(serviceProvider));
            _packetResponseDictionary.Add(OneClickCraft.ProtocolTag, new OneClickCraft(serviceProvider));
            _packetResponseDictionary.Add(GetChallengeInfoProtocol.ProtocolTag, new GetChallengeInfoProtocol(serviceProvider));
            _packetResponseDictionary.Add(AllBlockStateProtocol.ProtocolTag, new AllBlockStateProtocol(serviceProvider));
            _packetResponseDictionary.Add(BlockStateProtocol.ProtocolTag, new BlockStateProtocol(serviceProvider));
            _packetResponseDictionary.Add(DebugBlockInfoRequestProtocol.ProtocolTag, new DebugBlockInfoRequestProtocol(serviceProvider));
            _packetResponseDictionary.Add(SetCraftChainerCrafterRecipeProtocol.ProtocolTag, new SetCraftChainerCrafterRecipeProtocol(serviceProvider));
        }
        
        public List<List<byte>> GetPacketResponse(List<byte> payload)
        {
            var request = MessagePackSerializer.Deserialize<ProtocolMessagePackBase>(payload.ToArray());
            var response = _packetResponseDictionary[request.Tag].GetResponse(payload);
            
            if (response == null) return new List<List<byte>>();
            
            response.SequenceId = request.SequenceId;
            var responseBytes = MessagePackSerializer.Serialize(Convert.ChangeType(response, response.GetType()));
            
            return new List<List<byte>> { responseBytes.ToList() };
        }
    }
}