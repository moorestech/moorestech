using System;
using System.Collections.Generic;
using System.Linq;
using Game.PlayerInventory.Interface;
using MessagePack;
using Server.Util.MessagePack;

namespace Server.Protocol.PacketResponse
{
    public class PlayerInventoryResponseProtocol : IPacketResponse
    {
        public const string Tag = "va:playerInvRequest";

        private readonly IPlayerInventoryDataStore _playerInventoryDataStore;

        public PlayerInventoryResponseProtocol(IPlayerInventoryDataStore playerInventoryDataStore)
        {
            _playerInventoryDataStore = playerInventoryDataStore;
        }

        public List<List<byte>> GetResponse(List<byte> payload)
        {
            var data = MessagePackSerializer.Deserialize<PlayerInventoryResponseProtocolMessagePack>(payload.ToArray());

            var playerInventory = _playerInventoryDataStore.GetInventoryData(data.PlayerId);

            //メインインベントリのアイテムを設定
            var mainItems = new List<ItemMessagePack>();
            for (var i = 0; i < PlayerInventoryConst.MainInventorySize; i++)
            {
                var id = playerInventory.MainOpenableInventory.GetItem(i).Id;
                var count = playerInventory.MainOpenableInventory.GetItem(i).Count;
                mainItems.Add(new ItemMessagePack(id, count));
            }

            //グラブインベントリのアイテムを設定
            var grabItem = new ItemMessagePack(
                playerInventory.GrabInventory.GetItem(0).Id,
                playerInventory.GrabInventory.GetItem(0).Count);

            var response = MessagePackSerializer.Serialize(new PlayerInventoryResponseProtocolMessagePack(
                data.PlayerId, mainItems.ToArray(), grabItem));

            return new List<List<byte>> { response.ToList() };
        }
    }


    [MessagePackObject(true)]
    public class RequestPlayerInventoryProtocolMessagePack : ProtocolMessagePackBase
    {
        [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
        public RequestPlayerInventoryProtocolMessagePack()
        {
        }

        public RequestPlayerInventoryProtocolMessagePack(int playerId)
        {
            Tag = PlayerInventoryResponseProtocol.Tag;
            PlayerId = playerId;
        }

        public int PlayerId { get; set; }
    }


    [MessagePackObject(true)]
    public class PlayerInventoryResponseProtocolMessagePack : ProtocolMessagePackBase
    {
        [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
        public PlayerInventoryResponseProtocolMessagePack()
        {
        }


        public PlayerInventoryResponseProtocolMessagePack(int playerId, ItemMessagePack[] main, ItemMessagePack grab)
        {
            Tag = PlayerInventoryResponseProtocol.Tag;
            PlayerId = playerId;
            Main = main;
            Grab = grab;
        }

        public int PlayerId { get; set; }

        public ItemMessagePack[] Main { get; set; }
        public ItemMessagePack Grab { get; set; }
    }
}