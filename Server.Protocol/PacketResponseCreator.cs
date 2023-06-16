using System;
using System.Collections.Generic;
using System.Linq;
using Game.PlayerInventory.Interface;
using Game.WorldMap;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using Server.Event;
using Server.Event.EventReceive;
using Server.Protocol.Base;
using Server.Protocol.PacketResponse;
using Server.Util;

namespace Server.Protocol
{
    public class PacketResponseCreator
    {
        private readonly Dictionary<string,IPacketResponse> _packetResponseDictionary = new();

        //この辺もDIコンテナに載せる?
        public PacketResponseCreator(ServiceProvider serviceProvider)
        {
            _packetResponseDictionary.Add(DummyProtocol.Tag,new DummyProtocol());
            _packetResponseDictionary.Add(InitialHandshakeProtocol.Tag,new InitialHandshakeProtocol(serviceProvider));
            _packetResponseDictionary.Add(PlayerCoordinateSendProtocol.Tag,new PlayerCoordinateSendProtocol(serviceProvider));
            _packetResponseDictionary.Add(PlayerInventoryResponseProtocol.Tag,new PlayerInventoryResponseProtocol(serviceProvider.GetService<IPlayerInventoryDataStore>()));
            _packetResponseDictionary.Add(EventProtocolMessagePackBase.EventProtocolTag,new EventProtocol(serviceProvider.GetService<EventProtocolProvider>()));
            _packetResponseDictionary.Add(InventoryItemMoveProtocol.Tag,new InventoryItemMoveProtocol(serviceProvider));
            _packetResponseDictionary.Add(SendPlaceHotBarBlockProtocol.Tag,new SendPlaceHotBarBlockProtocol(serviceProvider));
            _packetResponseDictionary.Add(BlockInventoryRequestProtocol.Tag,new BlockInventoryRequestProtocol(serviceProvider));
            _packetResponseDictionary.Add(RemoveBlockProtocol.Tag,new RemoveBlockProtocol(serviceProvider));
            _packetResponseDictionary.Add(SendCommandProtocol.Tag,new SendCommandProtocol(serviceProvider));
            _packetResponseDictionary.Add(CraftProtocol.Tag,new CraftProtocol(serviceProvider));
            _packetResponseDictionary.Add(MiningOperationProtocol.Tag,new MiningOperationProtocol(serviceProvider)); 
            _packetResponseDictionary.Add(BlockInventoryOpenCloseProtocol.Tag,new BlockInventoryOpenCloseProtocol(serviceProvider));
            _packetResponseDictionary.Add(SaveProtocol.Tag,new SaveProtocol(serviceProvider));
            _packetResponseDictionary.Add(QuestProgressRequestProtocol.Tag,new QuestProgressRequestProtocol(serviceProvider));
            _packetResponseDictionary.Add(EarnQuestRewardProtocol.Tag,new EarnQuestRewardProtocol(serviceProvider));
            _packetResponseDictionary.Add(SetRecipeCraftingInventoryProtocol.Tag,new SetRecipeCraftingInventoryProtocol(serviceProvider));
            _packetResponseDictionary.Add(MapObjectDestructionInformationProtocol.Tag,new MapObjectDestructionInformationProtocol(serviceProvider));
            _packetResponseDictionary.Add(GetMapObjectProtocol.Tag,new GetMapObjectProtocol(serviceProvider));

            serviceProvider.GetService<VeinGenerator>();
        }

        public  List<List<byte>> GetPacketResponse(List<byte> payload)
        {
            var tag = MessagePackSerializer.Deserialize<ToServerProtocolMessagePackBase>(payload.ToArray()).ToServerTag;
            if (string.IsNullOrEmpty(tag) || !_packetResponseDictionary.ContainsKey(tag))
            {
                var json = MessagePackSerializer.ConvertToJson(payload.ToArray());
                //TODO ログ出力
                Console.WriteLine("リクエストのタグが設定されていません " + json);
                return new List<List<byte>>();
            }
            
            var toClientPacks = _packetResponseDictionary[tag].GetResponse(payload);
            var response = new List<List<byte>>();
            foreach (var toClient in toClientPacks)
            {
                if (string.IsNullOrEmpty(toClient.ToClientTag))
                {
                    //TODO ログ出力
                    Console.WriteLine("レスポンスのタグが設定されていません " + tag);
                    continue;
                }
                var bytes = MessagePackSerializer.Serialize(toClient);
                response.Add(bytes.ToList());
            }

            return response;
        }
    }
}