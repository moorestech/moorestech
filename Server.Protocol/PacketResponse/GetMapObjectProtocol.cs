using System;
using System.Collections.Generic;
using Core.Const;
using Core.Item;
using Game.MapObject.Interface;
using Game.PlayerInventory.Interface;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;

namespace Server.Protocol.PacketResponse
{
    /// <summary>
    ///     MapObjectを取得するときのプロトコル
    /// </summary>
    public class GetMapObjectProtocol : IPacketResponse
    {
        public const string Tag = "va:getMapObjectInfo";
        private readonly ItemStackFactory _itemStackFactory;

        private readonly IMapObjectDatastore _mapObjectDatastore;
        private readonly IPlayerInventoryDataStore _playerInventoryDataStore;

        public GetMapObjectProtocol(ServiceProvider serviceProvider)
        {
            _mapObjectDatastore = serviceProvider.GetService<IMapObjectDatastore>();
            _itemStackFactory = serviceProvider.GetService<ItemStackFactory>();
            _playerInventoryDataStore = serviceProvider.GetService<IPlayerInventoryDataStore>();
        }


        public List<List<byte>> GetResponse(List<byte> payload)
        {
            var data = MessagePackSerializer.Deserialize<GetMapObjectProtocolProtocolMessagePack>(payload.ToArray());

            var mapObject = _mapObjectDatastore.Get(data.InstanceId);
            var itemStack = _itemStackFactory.Create(mapObject.ItemId, mapObject.ItemCount);
            var playerMainInventory =
                _playerInventoryDataStore.GetInventoryData(data.PlayerId).MainOpenableInventory;
            var insertedItem = playerMainInventory.InsertItem(itemStack);

            //アイテムの挿入に成功したらマップオブジェクトを削除
            if (insertedItem.Id == ItemConst.EmptyItemId) mapObject.Destroy();

            return new List<List<byte>>();
        }
    }


    [MessagePackObject(true)]
    public class GetMapObjectProtocolProtocolMessagePack : ProtocolMessagePackBase
    {
        [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
        public GetMapObjectProtocolProtocolMessagePack()
        {
        }

        public GetMapObjectProtocolProtocolMessagePack(int playerId, int instanceId)
        {
            Tag = GetMapObjectProtocol.Tag;
            PlayerId = playerId;
            InstanceId = instanceId;
        }

        public int PlayerId { get; set; }
        public int InstanceId { get; set; }
    }
}