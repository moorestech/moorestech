using System;
using System.Collections.Generic;
using System.Linq;
using Game.PlayerInventory.Interface;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using Server.Event;
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
            _packetResponseDictionary.Add(InitialHandshakeProtocol.ProtocolTag, new InitialHandshakeProtocol(serviceProvider));
            _packetResponseDictionary.Add(RequestWorldDataProtocol.ProtocolTag, new RequestWorldDataProtocol(serviceProvider));
            _packetResponseDictionary.Add(PlayerInventoryResponseProtocol.ProtocolTag, new PlayerInventoryResponseProtocol(serviceProvider));
            _packetResponseDictionary.Add(SetPlayerCoordinateProtocol.ProtocolTag, new SetPlayerCoordinateProtocol(serviceProvider));
            _packetResponseDictionary.Add(GearChainConnectionEditProtocol.Tag, new GearChainConnectionEditProtocol(serviceProvider));
            _packetResponseDictionary.Add(EventProtocol.ProtocolTag, new EventProtocol(serviceProvider.GetService<EventProtocolProvider>()));
            _packetResponseDictionary.Add(InventoryItemMoveProtocol.ProtocolTag, new InventoryItemMoveProtocol(serviceProvider));
            _packetResponseDictionary.Add(PlaceBlockFromHotBarProtocol.ProtocolTag, new PlaceBlockFromHotBarProtocol(serviceProvider));
            _packetResponseDictionary.Add(InventoryRequestProtocol.ProtocolTag, new InventoryRequestProtocol(serviceProvider));
            _packetResponseDictionary.Add(RemoveBlockProtocol.ProtocolTag, new RemoveBlockProtocol(serviceProvider));
            _packetResponseDictionary.Add(CompleteBaseCampProtocol.ProtocolTag, new CompleteBaseCampProtocol(serviceProvider));
            _packetResponseDictionary.Add(SendCommandProtocol.ProtocolTag, new SendCommandProtocol(serviceProvider));
            _packetResponseDictionary.Add(SubscribeInventoryProtocol.ProtocolTag, new SubscribeInventoryProtocol(serviceProvider));
            _packetResponseDictionary.Add(SaveProtocol.ProtocolTag, new SaveProtocol(serviceProvider));
            _packetResponseDictionary.Add(GetMapObjectInfoProtocol.ProtocolTag, new GetMapObjectInfoProtocol(serviceProvider));
            _packetResponseDictionary.Add(GetGameUnlockStateProtocol.ProtocolTag, new GetGameUnlockStateProtocol(serviceProvider));
            _packetResponseDictionary.Add(MapObjectAcquisitionProtocol.ProtocolTag, new MapObjectAcquisitionProtocol(serviceProvider));
            _packetResponseDictionary.Add(OneClickCraft.ProtocolTag, new OneClickCraft(serviceProvider));
            _packetResponseDictionary.Add(GetChallengeInfoProtocol.ProtocolTag, new GetChallengeInfoProtocol(serviceProvider));
            _packetResponseDictionary.Add(AllBlockStateProtocol.ProtocolTag, new AllBlockStateProtocol(serviceProvider));
            _packetResponseDictionary.Add(BlockStateProtocol.ProtocolTag, new BlockStateProtocol(serviceProvider));
            _packetResponseDictionary.Add(InvokeBlockStateEventProtocol.ProtocolTag, new InvokeBlockStateEventProtocol(serviceProvider));
            _packetResponseDictionary.Add(DebugBlockInfoRequestProtocol.ProtocolTag, new DebugBlockInfoRequestProtocol(serviceProvider));
            _packetResponseDictionary.Add(SetCraftChainerCrafterRecipeProtocol.ProtocolTag, new SetCraftChainerCrafterRecipeProtocol(serviceProvider));
            _packetResponseDictionary.Add(SetCraftChainerMainComputerRequestItemProtocol.ProtocolTag, new SetCraftChainerMainComputerRequestItemProtocol(serviceProvider));
            _packetResponseDictionary.Add(ApplyCraftTreeProtocol.ProtocolTag, new ApplyCraftTreeProtocol(serviceProvider));
            _packetResponseDictionary.Add(GetCraftTreeProtocol.ProtocolTag, new GetCraftTreeProtocol(serviceProvider));
            _packetResponseDictionary.Add(RegisterPlayedSkitProtocol.ProtocolTag, new RegisterPlayedSkitProtocol(serviceProvider));
            _packetResponseDictionary.Add(GetFluidInventoryProtocol.ProtocolTag, new GetFluidInventoryProtocol(serviceProvider));
            _packetResponseDictionary.Add(CompleteResearchProtocol.ProtocolTag, new CompleteResearchProtocol(serviceProvider));
            _packetResponseDictionary.Add(GetResearchInfoProtocol.ProtocolTag, new GetResearchInfoProtocol(serviceProvider));
            _packetResponseDictionary.Add(GetPlayedSkitIdsProtocol.ProtocolTag, new GetPlayedSkitIdsProtocol(serviceProvider));
            _packetResponseDictionary.Add(RailConnectionEditProtocol.Tag, new RailConnectionEditProtocol(serviceProvider));
            _packetResponseDictionary.Add(GetRailGraphSnapshotProtocol.ProtocolTag, new GetRailGraphSnapshotProtocol());
            _packetResponseDictionary.Add(PlaceTrainCarOnRailProtocol.ProtocolTag, new PlaceTrainCarOnRailProtocol(serviceProvider));
            _packetResponseDictionary.Add(RemoveTrainCarProtocol.ProtocolTag, new RemoveTrainCarProtocol());
        }
        
        public List<List<byte>> GetPacketResponse(List<byte> payload)
        {
            ProtocolMessagePackBase request = null;
            ProtocolMessagePackBase response = null;
            try
            {
                request = MessagePackSerializer.Deserialize<ProtocolMessagePackBase>(payload.ToArray());
                response = _packetResponseDictionary[request.Tag].GetResponse(payload);
            }
            catch (Exception e)
            {
                // TODO ログ基盤
                Debug.LogError($"PacketResponseCreator Error:{e.Message}\n{e.StackTrace}");
            }
            
            if (response == null) return new List<List<byte>>();
            
            response.SequenceId = request.SequenceId;
            var responseBytes = MessagePackSerializer.Serialize(Convert.ChangeType(response, response.GetType()));
            
            return new List<List<byte>> { responseBytes.ToList() };
        }
    }
}
